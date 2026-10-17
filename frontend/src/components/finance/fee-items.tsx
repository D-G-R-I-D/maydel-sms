"use client";

import { useState } from "react";
import { Copy, Pencil, Plus, Trash2, Users, Wallet } from "lucide-react";
import { problemMessage } from "@/lib/api/problem";
import type { FeeItem } from "@/lib/api/types";
import { copyFeeItems, deleteFeeItem } from "@/lib/finance/actions";
import { formatNaira, groupFeeItems, groupTotal, optionalCount } from "@/lib/finance/rules";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Card, Select } from "@/components/ui/primitives";
import { EmptyState } from "@/components/ui/states";
import { CancelButton, ConfirmDialog, DialogActions, FormDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";
import { FeeItemForm } from "./fee-item-form";
import { OptInSheet } from "./opt-in-sheet";

type Level = { id: string; name: string };
type TermChoice = { id: string; label: string };

/** Fee items for one term, grouped "All classes" then by level (E10 part 1). */
export function FeeItems({ termId, termLabel, items, levels, otherTerms }: { termId: string; termLabel: string; items: FeeItem[]; levels: Level[]; otherTerms: TermChoice[] }) {
  const [editing, setEditing] = useState<FeeItem | "new" | null>(null);
  const [optIns, setOptIns] = useState<FeeItem | null>(null);
  const [removing, setRemoving] = useState<FeeItem | null>(null);
  const groups = groupFeeItems(items);

  const addButton = (
    <Button onClick={() => setEditing("new")}>
      <Plus className="size-4" aria-hidden />
      Add fee
    </Button>
  );

  return (
    <div className="space-y-5">
      {items.length === 0 ? (
        <Card>
          <EmptyState icon={Wallet} title={`No fees for ${termLabel} yet`} action={<div className="flex flex-wrap justify-center gap-2">{addButton}{otherTerms.length > 0 && <CopyFromTerm toTermId={termId} terms={otherTerms} />}</div>}>
            Add tuition and other fees, or copy last term&apos;s fees and adjust them.
          </EmptyState>
        </Card>
      ) : (
        <>
          <div className="flex flex-wrap justify-end gap-2">{addButton}</div>
          {groups.map((g) => (
            <Card key={g.key} className="overflow-hidden">
              <div className="flex flex-wrap items-baseline justify-between gap-2 border-b border-border px-5 py-3">
                <h2 className="font-display text-lg font-semibold">{g.label}</h2>
                <p className="text-sm text-muted">
                  Mandatory total <span className="tabular font-medium text-text">{formatNaira(groupTotal(g.items))}</span>
                </p>
              </div>
              <ul className="divide-y divide-border">
                {g.items.map((item) => (
                  <li key={item.id} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-5 py-3">
                    <div className="min-w-0 flex-1 basis-48">
                      <p className="font-medium">{item.name}</p>
                      <p className="mt-1 flex flex-wrap gap-1.5">
                        <Badge tone={item.isMandatory ? "brand" : "neutral"}>{item.isMandatory ? "Mandatory" : "Optional"}</Badge>
                        {item.audience === "NewIntake" && <Badge tone="brand">New intake</Badge>}
                        {item.audience === "Returning" && <Badge tone="brand">Returning</Badge>}
                        {!item.isMandatory && item.chargedByDefault && <Badge>On by default</Badge>}
                        {item.siblingDiscount && <Badge tone="success">Sibling discount</Badge>}
                        {!item.isMandatory && <Badge tone={!item.chargedByDefault && item.optedIn === 0 ? "warning" : "neutral"}>{optionalCount(item)}</Badge>}
                      </p>
                    </div>
                    <p className="tabular w-36 text-right font-semibold">{formatNaira(item.amount)}</p>
                    <div className="flex shrink-0 gap-1">
                      {!item.isMandatory && (
                        <Button variant="secondary" size="sm" onClick={() => setOptIns(item)}>
                          <Users className="size-4" aria-hidden />
                          Who pays this?
                        </Button>
                      )}
                      <Button variant="ghost" size="sm" onClick={() => setEditing(item)} aria-label={`Edit ${item.name}`}>
                        <Pencil className="size-4" aria-hidden />
                      </Button>
                      <Button variant="ghost" size="sm" className="text-danger" onClick={() => setRemoving(item)} aria-label={`Delete ${item.name}`}>
                        <Trash2 className="size-4" aria-hidden />
                      </Button>
                    </div>
                  </li>
                ))}
              </ul>
            </Card>
          ))}
        </>
      )}

      <FormDialog open={editing !== null} onOpenChange={(v) => !v && setEditing(null)} title={editing === "new" ? "Add fee" : "Edit fee"} description={termLabel}>
        {(close) => editing !== null && <FeeItemForm termId={termId} levels={levels} item={editing === "new" ? undefined : editing} close={close} />}
      </FormDialog>

      <FormDialog
        open={optIns !== null}
        onOpenChange={(v) => !v && setOptIns(null)}
        title={optIns ? `Who pays ${optIns.name}?` : "Who pays this?"}
        description={
          optIns
            ? `${formatNaira(optIns.amount)} · ${optIns.classLevel ?? "All classes"} · ${optIns.chargedByDefault ? "everyone pays unless you untick them" : "only ticked students pay"}`
            : undefined
        }
        size="lg"
      >
        {(close) => optIns && <OptInSheet item={optIns} close={close} />}
      </FormDialog>

      <ConfirmDialog
        open={removing !== null}
        onOpenChange={(v) => !v && setRemoving(null)}
        title={`Delete ${removing?.name ?? "fee"}?`}
        confirmLabel="Delete fee"
        pendingLabel="Deleting…"
        tone="destructive"
        onConfirm={async () => {
          if (!removing) return true;
          const ok = await runAction(() => deleteFeeItem(removing.id), {
            success: `${removing.name} deleted`,
            onProblem: (p) => p.status > 0 && p.status < 500 && p.status !== 429 && toast.error("Couldn't delete the fee", { description: problemMessage(p) }),
          });
          return ok !== null;
        }}
      >
        <p>New invoices won&apos;t include it. Invoices already issued keep their lines.</p>
      </ConfirmDialog>
    </div>
  );
}

function CopyFromTerm({ toTermId, terms }: { toTermId: string; terms: TermChoice[] }) {
  const [from, setFrom] = useState(terms[0]?.id ?? "");
  const [busy, setBusy] = useState(false);
  return (
    <FormDialog
      title="Copy fees from another term"
      description="Copies every fee item and who takes the optional ones. You can edit them afterwards."
      trigger={
        <Button variant="secondary">
          <Copy className="size-4" aria-hidden />
          Copy from another term
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4">
          <label className="block space-y-1.5 text-sm font-medium" htmlFor="copy-from">
            Copy from
          </label>
          <Select id="copy-from" value={from} onChange={(e) => setFrom(e.target.value)}>
            {terms.map((t) => (
              <option key={t.id} value={t.id}>
                {t.label}
              </option>
            ))}
          </Select>
          <DialogActions>
            <CancelButton />
            <Button
              loading={busy}
              loadingText="Copying…"
              disabled={!from}
              onClick={async () => {
                setBusy(true);
                const r = await runAction(() => copyFeeItems(from, toTermId), {
                  success: (x) => `${x.length} ${x.length === 1 ? "fee" : "fees"} copied`,
                  onProblem: (p) => p.status > 0 && p.status < 500 && p.status !== 429 && toast.error("Couldn't copy fees", { description: problemMessage(p) }),
                });
                setBusy(false);
                if (r) close();
              }}
            >
              <Copy className="size-4" aria-hidden />
              Copy fees
            </Button>
          </DialogActions>
        </div>
      )}
    </FormDialog>
  );
}
