"use client";

import { useDraft } from "@/lib/drafts/use-draft";
import { DraftBanner } from "@/components/drafts/draft-banner";
import { useState } from "react";
import { ArrowDown, ArrowUp, GripVertical, Plus, Save, Trash2, Undo2 } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { BehaviourSettings } from "@/lib/api/types";
import { saveBehaviourSettings } from "@/lib/assessment/actions";
import { move } from "@/lib/assessment/settings";
import { runAction } from "@/lib/client/action";
import { traitIssues } from "@/lib/reports/behaviour";
import { Badge, Button, Card, Input, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Switch } from "@/components/ui/switch";

type Row = { key: string; id: string | null; name: string; isActive: boolean; inUse: boolean };
let seq = 0;
const toRows = (s: BehaviourSettings): Row[] => [...s.traits].sort((a, b) => a.order - b.order).map((t) => ({ key: `t${++seq}`, id: t.id, name: t.name, isActive: t.isActive, inUse: t.inUse }));
const payload = (rows: readonly Row[]) => rows.map((r) => ({ id: r.id, name: r.name.trim(), isActive: r.isActive }));

/** Behaviour traits (E2.5): reorder (= report order), rename, toggle active, add, remove. Admin only. */
export function TraitsEditor({ initial, canEdit }: { initial: BehaviourSettings; canEdit: boolean }) {
  const [base, setBase] = useState(() => toRows(initial));
  const [rows, setRows] = useState<Row[]>(base);
  const [dragging, setDragging] = useState<number | null>(null);
  const [removing, setRemoving] = useState<Row | null>(null);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [saving, setSaving] = useState(false);

  const issues = traitIssues(rows);
  const dirty = JSON.stringify(payload(rows)) !== JSON.stringify(payload(base));
  const draft = useDraft({ scope: ["behaviour-traits"], value: rows, dirty, server: payload(base), editable: canEdit, onRestore: setRows });
  const discard = () => {
    draft.clear();
    setRows(base);
  };
  const set = (i: number, patch: Partial<Row>) => setRows((l) => l.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  const remove = (row: Row) => setRows((l) => l.filter((r) => r.key !== row.key));

  const save = async () => {
    setProblem(null);
    setSaving(true);
    const saved = await runAction(() => saveBehaviourSettings(payload(rows)), { success: "Behaviour traits saved", onProblem: setProblem });
    setSaving(false);
    if (saved) {
      const next = toRows(saved);
      setBase(next);
      setRows(next);
      draft.clear();
    }
  };

  return (
    <div className="space-y-5">
      <DraftBanner draft={draft} onDiscard={discard} />
      <Card className="overflow-hidden">
        <div className="border-b border-border px-5 py-4">
          <h2 className="font-display text-lg font-semibold">Behaviour traits</h2>
          <p className="text-sm text-muted">Rated A–E on each report, in this order. They never affect scores or positions.</p>
          <ul className="mt-2 flex flex-wrap gap-1.5 text-xs" aria-label="Rating key (fixed)">
            {initial.ratingKey.map((k) => (
              <li key={k.rating} className="rounded-full bg-surface-2 px-2.5 py-0.5">
                <span className="font-semibold">{k.rating}</span> {k.meaning}
              </li>
            ))}
          </ul>
        </div>
        <ul className="divide-y divide-border">
          {rows.map((r, i) => (
            <li
              key={r.key}
              draggable={canEdit}
              onDragStart={() => setDragging(i)}
              onDragOver={(e) => {
                if (dragging === null || dragging === i) return;
                e.preventDefault();
                setRows((l) => move(l, dragging, i));
                setDragging(i);
              }}
              onDragEnd={() => setDragging(null)}
              className={cn("flex flex-wrap items-center gap-2 px-3 py-2 sm:flex-nowrap sm:px-5", dragging === i && "bg-brand/5", !r.isActive && "opacity-70")}
            >
              {canEdit ? (
                <>
                  <GripVertical className="hidden size-4 cursor-grab text-muted sm:block" aria-hidden />
                  <div className="flex flex-col">
                    <button type="button" className="rounded p-0.5 text-muted hover:bg-surface-2 hover:text-text disabled:opacity-30" disabled={i === 0} onClick={() => setRows((l) => move(l, i, i - 1))} aria-label={`Move ${r.name || "trait"} up`}>
                      <ArrowUp className="size-3.5" aria-hidden />
                    </button>
                    <button type="button" className="rounded p-0.5 text-muted hover:bg-surface-2 hover:text-text disabled:opacity-30" disabled={i === rows.length - 1} onClick={() => setRows((l) => move(l, i, i + 1))} aria-label={`Move ${r.name || "trait"} down`}>
                      <ArrowDown className="size-3.5" aria-hidden />
                    </button>
                  </div>
                  <div className="min-w-[10rem] flex-1">
                    <Input value={r.name} onChange={(e) => set(i, { name: e.target.value })} aria-label="Trait name" placeholder="e.g. Punctual" maxLength={70} className="h-10 min-h-0 px-2 sm:text-sm" />
                  </div>
                  {r.inUse && <Badge className="hidden sm:inline-flex">In use</Badge>}
                  <label className="flex items-center gap-2 text-sm text-muted">
                    <Switch checked={r.isActive} onCheckedChange={(v) => set(i, { isActive: v })} label={`${r.name || "Trait"} active`} />
                    <span className="w-14">{r.isActive ? "Active" : "Hidden"}</span>
                  </label>
                  <Button variant="ghost" size="sm" className="size-10 px-0" onClick={() => (r.inUse ? setRemoving(r) : remove(r))} aria-label={`Remove ${r.name || "trait"}`}>
                    <Trash2 className="size-4 text-danger" aria-hidden />
                  </Button>
                </>
              ) : (
                <>
                  <span className="flex-1 text-sm">{r.name}</span>
                  {!r.isActive && <Badge>Hidden</Badge>}
                </>
              )}
            </li>
          ))}
        </ul>
        {canEdit && (
          <div className="border-t border-border px-5 py-3">
            <Button variant="secondary" size="sm" onClick={() => setRows((l) => [...l, { key: `t${++seq}`, id: null, name: "", isActive: true, inUse: false }])}>
              <Plus className="size-4" aria-hidden />
              Add trait
            </Button>
          </div>
        )}
      </Card>

      <ConfirmDialog
        open={removing !== null}
        onOpenChange={(v) => !v && setRemoving(null)}
        title={`Remove "${removing?.name ?? ""}"?`}
        confirmLabel="Remove"
        pendingLabel="Removing…"
        tone="destructive"
        onConfirm={async () => {
          if (removing) remove(removing);
          return true;
        }}
      >
        <p>It has already been rated on some reports, so it will be hidden from new reports but kept on old ones. Takes effect when you save.</p>
      </ConfirmDialog>

      {canEdit && (
        <div className="sticky bottom-4 z-10">
          <Card className={cn("flex flex-col gap-3 p-3 sm:flex-row sm:items-center", dirty && "border-brand/40 shadow-lg")}>
            <div className="flex-1 text-sm" aria-live="polite">
              {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 ? (
                <span className="text-danger">{problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}</span>
              ) : issues.length > 0 && dirty ? (
                <ul className="list-disc pl-4 text-danger">
                  {issues.slice(0, 3).map((x) => (
                    <li key={x}>{x}</li>
                  ))}
                </ul>
              ) : (
                <span className="text-muted">{dirty ? "Unsaved changes" : "No changes"}</span>
              )}
            </div>
            <div className="flex gap-2">
              <Button variant="secondary" onClick={discard} disabled={!dirty || saving}>
                <Undo2 className="size-4" aria-hidden />
                Undo changes
              </Button>
              <Button onClick={save} loading={saving} loadingText="Saving…" disabled={!dirty || issues.length > 0}>
                <Save className="size-4" aria-hidden />
                Save traits
              </Button>
            </div>
          </Card>
        </div>
      )}
      {!canEdit && <Alert tone="info">View only. Ask an administrator to change the traits.</Alert>}
    </div>
  );
}
