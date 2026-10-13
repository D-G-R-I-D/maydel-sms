"use client";

import { useState } from "react";
import { ArrowDown, ArrowUp, GripVertical, Lock, Plus, Save, Trash2, Undo2 } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { AssessmentSettings } from "@/lib/api/types";
import { saveAssessmentSettings } from "@/lib/assessment/actions";
import { activeTotal, blankBand, blankComponent, move, settingsIssues, toPayload, toRows, type BandRow, type ComponentRow } from "@/lib/assessment/settings";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Card, Input, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";

const cellInput = "h-10 min-h-0 px-2 sm:text-sm";

/** Components (reorderable, must total 100) and the grading scale. Admin edits; teachers read. */
export function SettingsEditor({ initial, canEdit }: { initial: AssessmentSettings; canEdit: boolean }) {
  const [base, setBase] = useState(() => toRows(initial));
  const [components, setComponents] = useState<ComponentRow[]>(base.components);
  const [bands, setBands] = useState<BandRow[]>(base.bands);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [saving, setSaving] = useState(false);
  const [dragging, setDragging] = useState<number | null>(null);

  const total = activeTotal(components);
  const issues = settingsIssues(components, bands);
  const dirty = JSON.stringify(toPayload(components, bands)) !== JSON.stringify(toPayload(base.components, base.bands));

  const setComp = (i: number, patch: Partial<ComponentRow>) => setComponents((list) => list.map((c, j) => (j === i ? { ...c, ...patch } : c)));
  const setBand = (i: number, patch: Partial<BandRow>) => setBands((list) => list.map((b, j) => (j === i ? { ...b, ...patch } : b)));

  const save = async () => {
    setProblem(null);
    setSaving(true);
    const saved = await runAction(() => saveAssessmentSettings(toPayload(components, bands)), {
      success: "Assessment settings saved",
      onProblem: setProblem,
    });
    setSaving(false);
    if (saved) {
      const rows = toRows(saved);
      setBase(rows);
      setComponents(rows.components);
      setBands(rows.bands);
    }
  };

  const reset = () => {
    setComponents(base.components);
    setBands(base.bands);
    setProblem(null);
  };

  return (
    <div className="space-y-5">
      <Alert tone="warning">Changing settings doesn&apos;t change reports that are already published.</Alert>

      <Card className="overflow-hidden">
        <div className="flex flex-wrap items-center gap-3 border-b border-border px-5 py-4">
          <div className="flex-1">
            <h2 className="font-display text-lg font-semibold">Score components</h2>
            <p className="text-sm text-muted">In report-card order. Active components must total 100.</p>
          </div>
          <p
            className={cn("tabular rounded-full px-3 py-1 text-sm font-semibold", total === 100 ? "bg-success/12 text-success" : "bg-danger/10 text-danger")}
            aria-live="polite"
          >
            Total {total} / 100
          </p>
        </div>
        <ul className="divide-y divide-border">
          {components.map((c, i) => (
            <li
              key={c.key}
              draggable={canEdit}
              onDragStart={() => setDragging(i)}
              onDragOver={(e) => {
                if (dragging === null || dragging === i) return;
                e.preventDefault();
                setComponents((list) => move(list, dragging, i));
                setDragging(i);
              }}
              onDragEnd={() => setDragging(null)}
              className={cn("flex flex-wrap items-center gap-2 px-3 py-2 sm:flex-nowrap sm:px-5", dragging === i && "bg-brand/5", !c.isActive && "opacity-70")}
            >
              {canEdit ? (
                <>
                  <GripVertical className="hidden size-4 cursor-grab text-muted sm:block" aria-hidden />
                  <div className="flex flex-col">
                    <button type="button" className="rounded p-0.5 text-muted hover:bg-surface-2 hover:text-text disabled:opacity-30" disabled={i === 0} onClick={() => setComponents((l) => move(l, i, i - 1))} aria-label={`Move ${c.name || "component"} up`}>
                      <ArrowUp className="size-3.5" aria-hidden />
                    </button>
                    <button type="button" className="rounded p-0.5 text-muted hover:bg-surface-2 hover:text-text disabled:opacity-30" disabled={i === components.length - 1} onClick={() => setComponents((l) => move(l, i, i + 1))} aria-label={`Move ${c.name || "component"} down`}>
                      <ArrowDown className="size-3.5" aria-hidden />
                    </button>
                  </div>
                  <Input value={c.name} onChange={(e) => setComp(i, { name: e.target.value })} aria-label="Component name" placeholder="e.g. CAT" className={cn(cellInput, "min-w-0 flex-1")} maxLength={50} />
                  <Input value={c.maxScore} onChange={(e) => setComp(i, { maxScore: e.target.value })} aria-label={`Max score for ${c.name || "component"}`} inputMode="decimal" className={cn(cellInput, "tabular w-20 text-center")} />
                  <label className="flex items-center gap-2 text-sm text-muted">
                    <Switch checked={c.isActive} onCheckedChange={(v) => setComp(i, { isActive: v })} label={`${c.name || "Component"} active`} />
                    <span className="w-14">{c.isActive ? "Active" : "Inactive"}</span>
                  </label>
                  {c.hasScores ? (
                    <span className="inline-flex size-9 items-center justify-center text-muted" title="Has scores: make it inactive instead of deleting">
                      <Lock className="size-4" aria-label="Has scores; can't be deleted" />
                    </span>
                  ) : (
                    <Button variant="ghost" size="sm" onClick={() => setComponents((l) => l.filter((_, j) => j !== i))} aria-label={`Delete ${c.name || "component"}`}>
                      <Trash2 className="size-4 text-danger" aria-hidden />
                    </Button>
                  )}
                </>
              ) : (
                <>
                  <span className="flex-1 font-medium">{c.name}</span>
                  <span className="tabular w-16 text-right">{c.maxScore}</span>
                  {!c.isActive && <Badge>Inactive</Badge>}
                </>
              )}
            </li>
          ))}
        </ul>
        {canEdit && (
          <div className="flex flex-wrap items-center gap-3 border-t border-border px-5 py-3">
            <Button variant="secondary" size="sm" onClick={() => setComponents((l) => [...l, blankComponent()])}>
              <Plus className="size-4" aria-hidden />
              Add component
            </Button>
            <p className="text-xs text-muted">Components with scores can&apos;t be deleted (lock icon); make them inactive instead.</p>
          </div>
        )}
      </Card>

      <Card className="overflow-hidden">
        <div className="border-b border-border px-5 py-4">
          <h2 className="font-display text-lg font-semibold">Grading scale</h2>
          <p className="text-sm text-muted">Highest first. The lowest band must start at 0.</p>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-surface-2 text-xs uppercase tracking-wide text-muted">
              <tr>
                <th scope="col" className="px-3 py-2 font-medium sm:px-5">Grade</th>
                <th scope="col" className="px-3 py-2 font-medium">Min</th>
                <th scope="col" className="px-3 py-2 font-medium">Max</th>
                <th scope="col" className="px-3 py-2 font-medium">Remark</th>
                {canEdit && <th scope="col" className="px-3 py-2"><span className="sr-only">Remove</span></th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {bands.map((b, i) =>
                canEdit ? (
                  <tr key={b.key}>
                    <td className="px-3 py-1.5 sm:px-5">
                      <Input value={b.grade} onChange={(e) => setBand(i, { grade: e.target.value })} aria-label="Grade" className={cn(cellInput, "w-16 text-center font-semibold")} maxLength={3} />
                    </td>
                    <td className="px-3 py-1.5">
                      <Input value={b.minScore} onChange={(e) => setBand(i, { minScore: e.target.value })} aria-label={`Min for ${b.grade || "band"}`} inputMode="decimal" className={cn(cellInput, "tabular w-20 text-center")} />
                    </td>
                    <td className="px-3 py-1.5">
                      <Input value={b.maxScore} onChange={(e) => setBand(i, { maxScore: e.target.value })} aria-label={`Max for ${b.grade || "band"}`} inputMode="decimal" className={cn(cellInput, "tabular w-20 text-center")} />
                    </td>
                    <td className="px-3 py-1.5">
                      <Input value={b.remark} onChange={(e) => setBand(i, { remark: e.target.value })} aria-label={`Remark for ${b.grade || "band"}`} className={cn(cellInput, "min-w-40")} maxLength={100} />
                    </td>
                    <td className="px-3 py-1.5">
                      <Button variant="ghost" size="sm" onClick={() => setBands((l) => l.filter((_, j) => j !== i))} aria-label={`Remove grade ${b.grade}`}>
                        <Trash2 className="size-4 text-danger" aria-hidden />
                      </Button>
                    </td>
                  </tr>
                ) : (
                  <tr key={b.key}>
                    <td className="px-3 py-2.5 font-semibold sm:px-5">{b.grade}</td>
                    <td className="tabular px-3 py-2.5">{b.minScore}</td>
                    <td className="tabular px-3 py-2.5">{b.maxScore}</td>
                    <td className="px-3 py-2.5">{b.remark}</td>
                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>
        {canEdit && (
          <div className="border-t border-border px-5 py-3">
            <Button variant="secondary" size="sm" onClick={() => setBands((l) => [...l, blankBand()])}>
              <Plus className="size-4" aria-hidden />
              Add band
            </Button>
          </div>
        )}
      </Card>

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
              <Button variant="secondary" onClick={reset} disabled={!dirty || saving}>
                <Undo2 className="size-4" aria-hidden />
                Undo changes
              </Button>
              <Button onClick={save} loading={saving} disabled={!dirty || issues.length > 0}>
                <Save className="size-4" aria-hidden />
                Save settings
              </Button>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
