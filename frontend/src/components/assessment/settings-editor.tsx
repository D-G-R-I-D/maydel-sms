"use client";

import { useDraft } from "@/lib/drafts/use-draft";
import { DraftBanner } from "@/components/drafts/draft-banner";
import { useState } from "react";
import { ArrowDown, ArrowUp, GripVertical, Lock, Plus, Save, Trash2, Undo2, Wand2 } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { AssessmentSettings, GradeBand, GradingPreset } from "@/lib/api/types";
import { saveAssessmentSettings } from "@/lib/assessment/actions";
import { activeTotal, blankBand, blankComponent, move, settingsIssues, toPayload, toRows, type BandRow, type ComponentRow } from "@/lib/assessment/settings";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Card, Input, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";
import { FormDialog } from "@/components/ui/dialog";

const cellInput = "h-10 min-h-0 px-2 sm:text-sm";
/** Band rows: phones = Grade | Min – Max, then Remark + remove; sm+ = one row with 7rem number columns. */
const bandColsMobile = "grid-cols-[4.5rem_minmax(0,1fr)_auto_minmax(0,1fr)]";
const bandCols = "sm:grid-cols-[5.5rem_7rem_7rem_minmax(0,1fr)_2.5rem]";
const bandColsRead = "sm:grid-cols-[5.5rem_7rem_7rem_minmax(0,1fr)]";

const bandKey = (bands: readonly GradeBand[]) =>
  JSON.stringify([...bands].sort((x, y) => y.minScore - x.minScore).map((b) => [b.grade, b.minScore, b.maxScore, b.remark]));

/** Pick a preset with every band previewed first; the one matching the saved scale is marked "In use". */
function PresetPicker({ presets, activeKey, onApply }: { presets: GradingPreset[]; activeKey: string | null; onApply: (p: GradingPreset) => void }) {
  return (
    <FormDialog
      size="lg"
      title="Choose a grading preset"
      description="Preview the bands, then fill the editor with one. Nothing is saved until you press Save settings."
      trigger={
        <Button variant="secondary" size="sm">
          <Wand2 className="size-4" aria-hidden />
          Use a preset
        </Button>
      }
    >
      {(close) => (
        <ul className="grid gap-3 md:grid-cols-2">
          {presets.map((p) => (
            <li key={p.key} className={cn("flex flex-col rounded-card border p-4", p.key === activeKey ? "border-brand/50 bg-brand/5" : "border-border")}>
              <div className="mb-1 flex items-center gap-2">
                <h3 className="flex-1 font-display text-base font-semibold">{p.name}</h3>
                {p.key === activeKey && <Badge tone="brand">In use</Badge>}
              </div>
              <p className="mb-3 text-xs text-muted">{p.description}</p>
              <table className="mb-4 w-full text-sm">
                <caption className="sr-only">{p.name} bands</caption>
                <tbody className="divide-y divide-border">
                  {[...p.bands]
                    .sort((x, y) => y.minScore - x.minScore)
                    .map((b) => (
                      <tr key={b.grade}>
                        <th scope="row" className="w-12 py-1 text-left font-semibold">
                          {b.grade}
                        </th>
                        <td className="tabular w-28 py-1 text-muted">
                          {b.minScore}–{b.maxScore}
                        </td>
                        <td className="py-1">{b.remark}</td>
                      </tr>
                    ))}
                </tbody>
              </table>
              <Button
                className="mt-auto"
                variant={p.key === activeKey ? "secondary" : "primary"}
                onClick={() => {
                  onApply(p);
                  close();
                }}
              >
                {p.key === activeKey ? "Reset to this preset" : "Use this preset"}
              </Button>
            </li>
          ))}
        </ul>
      )}
    </FormDialog>
  );
}

/** Components (reorderable, must total 100) and the grading scale. Admin edits; teachers read. */
export function SettingsEditor({ initial, canEdit, presets = [] }: { initial: AssessmentSettings; canEdit: boolean; presets?: GradingPreset[] }) {
  const [base, setBase] = useState(() => toRows(initial));
  const [components, setComponents] = useState<ComponentRow[]>(base.components);
  const [bands, setBands] = useState<BandRow[]>(base.bands);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [saving, setSaving] = useState(false);
  const [dragging, setDragging] = useState<number | null>(null);
  const [appliedPreset, setAppliedPreset] = useState<GradingPreset | null>(null);
  const savedKey = bandKey(toPayload(base.components, base.bands).bands);
  const activePreset = presets.find((p) => bandKey(p.bands) === savedKey) ?? null;

  const applyPreset = (preset: GradingPreset) => {
    setBands(toRows({ components: [], bands: preset.bands }).bands);
    setAppliedPreset(preset);
  };

  const total = activeTotal(components);
  const issues = settingsIssues(components, bands);
  const dirty = JSON.stringify(toPayload(components, bands)) !== JSON.stringify(toPayload(base.components, base.bands));

  const draft = useDraft({
    scope: ["assessment-settings"],
    value: { components, bands },
    dirty,
    server: toPayload(base.components, base.bands),
    editable: canEdit,
    onRestore: (v) => {
      setComponents(v.components);
      setBands(v.bands);
    },
  });

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
      draft.clear();
    }
  };

  const reset = () => {
    draft.clear();
    setComponents(base.components);
    setBands(base.bands);
    setAppliedPreset(null);
    setProblem(null);
  };

  return (
    <div className="space-y-5">
      <Alert tone="warning">Changing settings doesn&apos;t change reports that are already published.</Alert>
      <DraftBanner draft={draft} onDiscard={() => reset()} />

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
                  {/* Width lives on wrappers: Input carries w-full, which would win over a width class here. */}
                  <div className="min-w-[9rem] flex-1">
                    <Input value={c.name} onChange={(e) => setComp(i, { name: e.target.value })} aria-label="Component name" placeholder="e.g. CAT" className={cellInput} maxLength={50} />
                  </div>
                  <div className="w-28 shrink-0">
                    <Input value={c.maxScore} onChange={(e) => setComp(i, { maxScore: e.target.value })} aria-label={`Max score for ${c.name || "component"}`} inputMode="decimal" className={cn(cellInput, "tabular text-center")} />
                  </div>
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
        <div className="flex flex-wrap items-start gap-3 border-b border-border px-5 py-4">
          <div className="min-w-0 flex-1">
            <h2 className="font-display text-lg font-semibold">Grading scale</h2>
            <p className="text-sm text-muted">Highest first. The lowest band must start at 0. Only one scale is active at a time.</p>
            {activePreset && (
              <p className="mt-1.5">
                <Badge tone="brand">In use: {activePreset.name}</Badge>
              </p>
            )}
          </div>
          {canEdit && presets.length > 0 && <PresetPicker presets={presets} activeKey={activePreset?.key ?? null} onApply={applyPreset} />}
        </div>
        {appliedPreset && dirty && (
          <Alert tone="info" className="mx-5 mt-4" title={`${appliedPreset.name} filled in (not saved yet)`}>
            Applies to new and draft reports; published reports keep their figures. You can still edit the bands below, then save.
          </Alert>
        )}

        {/* Column headings from sm up; below that each band is a small card. */}
        <div className={cn("hidden gap-2 bg-surface-2 px-5 py-2 text-xs font-medium uppercase tracking-wide text-muted sm:grid", canEdit ? bandCols : bandColsRead)}>
          <span>Grade</span>
          <span>Min</span>
          <span>Max</span>
          <span>Remark</span>
          {canEdit && <span className="sr-only">Remove</span>}
        </div>
        <ul className="divide-y divide-border">
          {bands.map((b, i) =>
            canEdit ? (
              <li key={b.key} className={cn("grid items-center gap-2 px-4 py-3 sm:px-5 sm:py-2", bandColsMobile, bandCols)}>
                <Input value={b.grade} onChange={(e) => setBand(i, { grade: e.target.value })} aria-label="Grade" placeholder="A" className={cn(cellInput, "w-full text-center font-semibold")} maxLength={3} />
                <Input value={b.minScore} onChange={(e) => setBand(i, { minScore: e.target.value })} aria-label={`Min for ${b.grade || "band"}`} placeholder="Min" inputMode="decimal" className={cn(cellInput, "tabular w-full text-center")} />
                <span aria-hidden className="text-center text-muted sm:hidden">–</span>
                <Input value={b.maxScore} onChange={(e) => setBand(i, { maxScore: e.target.value })} aria-label={`Max for ${b.grade || "band"}`} placeholder="Max" inputMode="decimal" className={cn(cellInput, "tabular w-full text-center")} />
                <Input value={b.remark} onChange={(e) => setBand(i, { remark: e.target.value })} aria-label={`Remark for ${b.grade || "band"}`} placeholder="Remark, e.g. Excellent" className={cn(cellInput, "col-span-3 w-full sm:col-span-1")} maxLength={100} />
                <Button variant="ghost" size="sm" className="size-10 justify-self-end px-0" onClick={() => setBands((l) => l.filter((_, j) => j !== i))} aria-label={`Remove grade ${b.grade || "band"}`}>
                  <Trash2 className="size-4 text-danger" aria-hidden />
                </Button>
              </li>
            ) : (
              <li key={b.key} className={cn("grid grid-cols-[3.5rem_1fr] items-center gap-2 px-4 py-2.5 text-sm sm:px-5", bandColsRead)}>
                <span className="font-semibold">{b.grade}</span>
                <span className="tabular sm:hidden">
                  {b.minScore}–{b.maxScore} · <span className="text-muted">{b.remark}</span>
                </span>
                <span className="tabular hidden sm:block">{b.minScore}</span>
                <span className="tabular hidden sm:block">{b.maxScore}</span>
                <span className="hidden sm:block">{b.remark}</span>
              </li>
            ),
          )}
        </ul>
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
              <Button onClick={save} loading={saving} loadingText="Saving…" disabled={!dirty || issues.length > 0}>
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
