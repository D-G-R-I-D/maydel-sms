"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { History, PackageMinus, PackagePlus } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { formatNaira } from "@/lib/finance/rules";
import { formatLagos } from "@/lib/time/lagos";
import { adjustVariant, restockVariant, variantMovements } from "@/lib/store/actions";
import type { Movement, Variant } from "@/lib/store/store";
import { FormDialog } from "@/components/ui/dialog";
import { Button, Field, Input, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

/** Restock: quantity received, optional unit cost and supplier (kept in the history). */
export function RestockDialog({ variant, label }: { variant: Variant; label: string }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [qty, setQty] = useState("");
  const [cost, setCost] = useState("");
  const [supplier, setSupplier] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const qtyErr = /^\d{1,5}$/.test(qty) && Number(qty) > 0 ? undefined : "How many arrived?";
  const costErr = cost === "" || /^\d+(\.\d{1,2})?$/.test(cost) ? undefined : "An amount in naira.";
  const id = `rs-${variant.id}`;
  return (
    <FormDialog
      title={`Restock ${label}`}
      description={`In stock now: ${variant.stock}`}
      onOpenChange={(v) => v && (setQty(""), setCost(""), setSupplier(""), setProblem(null))}
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Restock ${label}`} title="Restock">
          <PackagePlus className="size-4" aria-hidden />
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4 p-5">
          {problem && <Alert tone="danger">{problemMessage(problem)}</Alert>}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id={`${id}-q`} label="Quantity received" error={qty ? qtyErr : undefined}>
              <Input value={qty} onChange={(e) => setQty(e.target.value)} inputMode="numeric" className="tabular" {...describedBy(`${id}-q`, qty ? qtyErr : undefined)} />
            </Field>
            <Field id={`${id}-c`} label="Unit cost (₦, optional)" error={costErr}>
              <Input value={cost} onChange={(e) => setCost(e.target.value)} inputMode="decimal" className="tabular" {...describedBy(`${id}-c`, costErr)} />
            </Field>
          </div>
          <Field id={`${id}-s`} label="Supplier (optional)">
            <Input id={`${id}-s`} value={supplier} onChange={(e) => setSupplier(e.target.value)} maxLength={120} />
          </Field>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Saving…"
              disabled={Boolean(qtyErr || costErr)}
              onClick={async () => {
                setBusy(true);
                const r = await runAction(() => restockVariant(variant.id, Number(qty), cost ? Number(cost) : null, supplier.trim() || null, null), {
                  success: (v) => `${label}: now ${v.stock} in stock`,
                  onProblem: setProblem,
                });
                setBusy(false);
                if (r) {
                  close();
                  startTransition(() => router.refresh());
                }
              }}
            >
              Add to stock
            </Button>
          </div>
        </div>
      )}
    </FormDialog>
  );
}

/** Adjust: add or remove stock (damaged, miscounted…) with a required reason. */
export function AdjustDialog({ variant, label }: { variant: Variant; label: string }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [change, setChange] = useState("");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const n = Number(change);
  const changeErr = /^-?\d{1,5}$/.test(change) && n !== 0 ? (variant.stock + n < 0 ? `Only ${variant.stock} in stock.` : undefined) : "e.g. -2 to remove two, 3 to add three.";
  const reasonErr = reason.trim() ? undefined : "Say why, e.g. damaged in the store.";
  const id = `aj-${variant.id}`;
  return (
    <FormDialog
      title={`Adjust ${label}`}
      description={`In stock now: ${variant.stock}`}
      onOpenChange={(v) => v && (setChange(""), setReason(""), setProblem(null))}
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Adjust stock of ${label}`} title="Adjust stock">
          <PackageMinus className="size-4" aria-hidden />
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4 p-5">
          {problem && <Alert tone="danger">{problemMessage(problem)}</Alert>}
          <Field id={`${id}-n`} label="Change" error={change ? changeErr : undefined} hint="Negative to remove, positive to add.">
            <Input value={change} onChange={(e) => setChange(e.target.value)} inputMode="numeric" className="tabular w-32" {...describedBy(`${id}-n`, change ? changeErr : undefined, true)} />
          </Field>
          <Field id={`${id}-r`} label="Reason" error={reason ? reasonErr : undefined}>
            <Input value={reason} onChange={(e) => setReason(e.target.value)} {...describedBy(`${id}-r`, reason ? reasonErr : undefined)} />
          </Field>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Saving…"
              disabled={Boolean(changeErr || reasonErr)}
              onClick={async () => {
                setBusy(true);
                const r = await runAction(() => adjustVariant(variant.id, n, reason.trim()), { success: (v) => `${label}: now ${v.stock} in stock`, onProblem: setProblem });
                setBusy(false);
                if (r) {
                  close();
                  startTransition(() => router.refresh());
                }
              }}
            >
              Save adjustment
            </Button>
          </div>
        </div>
      )}
    </FormDialog>
  );
}

const kindLabel: Record<Movement["kind"], string> = { Restock: "Restock", Sale: "Sold", Adjustment: "Adjustment", Reserve: "Held for order", Release: "Released" };

/** Every stock change for one option, newest first. */
export function MovementsDialog({ variant, label }: { variant: Variant; label: string }) {
  const [rows, setRows] = useState<Movement[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  return (
    <FormDialog
      size="lg"
      title={`${label}: stock history`}
      onOpenChange={async (v) => {
        if (!v) return;
        setRows(null);
        setError(null);
        try {
          const r = await variantMovements(variant.id);
          if (r.ok) setRows(r.data);
          else setError(problemMessage(r.problem));
        } catch {
          setError("Couldn't load the history.");
        }
      }}
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Stock history of ${label}`} title="Stock history">
          <History className="size-4" aria-hidden />
        </Button>
      }
    >
      {() => (
        <div className="p-5">
          {error ? (
            <Alert tone="danger">{error}</Alert>
          ) : !rows ? (
            <p className="text-sm text-muted">Loading…</p>
          ) : rows.length === 0 ? (
            <p className="text-sm text-muted">No stock changes yet.</p>
          ) : (
            <ul className="divide-y divide-border text-sm">
              {rows.map((m) => (
                <li key={m.id} className="flex flex-wrap items-center gap-x-3 gap-y-0.5 py-2">
                  <span className={cn("tabular w-12 text-right font-semibold", m.change > 0 ? "text-success" : "text-danger")}>{m.change > 0 ? `+${m.change}` : m.change}</span>
                  <span className="font-medium">{kindLabel[m.kind]}</span>
                  <span className="min-w-0 flex-1 truncate text-muted">
                    {[m.orderNumber, m.supplier, m.unitCost != null ? `${formatNaira(m.unitCost)} each` : null, m.note].filter(Boolean).join(" · ")}
                  </span>
                  <span className="text-xs text-muted">{formatLagos(m.createdAt)}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </FormDialog>
  );
}
