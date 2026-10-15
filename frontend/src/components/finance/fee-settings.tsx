"use client";

import { useState } from "react";
import { Minus, Plus, Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { FeeSettings, InstalmentPlan, PlanScope, SiblingDiscountMode } from "@/lib/api/types";
import { saveFeeSettings, saveInstalmentPlan } from "@/lib/finance/actions";
import { formatNaira, instalmentErrors, instalmentTotal, siblingErrors, siblingPays, type InstalmentRow } from "@/lib/finance/rules";
import { runAction } from "@/lib/client/action";
import { Button, Card, Field, Input, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";

const EXAMPLE_TUITION = 150_000;

const modes: { value: SiblingDiscountMode; label: string; hint: string }[] = [
  { value: "None", label: "No discount", hint: "Every child pays in full." },
  { value: "Percentage", label: "Percentage", hint: "e.g. 10% off for the 2nd child, 15% for the 3rd and later." },
  { value: "FixedAmount", label: "Fixed amount", hint: "e.g. ₦10,000 off for the 2nd child, ₦15,000 for each later child." },
];

function RadioCards<T extends string>({ name, value, options, onChange, disabled }: { name: string; value: T; options: { value: T; label: string; hint: string }[]; onChange: (v: T) => void; disabled: boolean }) {
  return (
    <div role="radiogroup" className="grid gap-2 sm:grid-cols-3">
      {options.map((o) => (
        <label key={o.value} htmlFor={`${name}-${o.value}`} className={cn("flex cursor-pointer gap-2.5 rounded-input border p-3 text-sm", value === o.value ? "border-brand bg-brand/5" : "border-border", disabled && "cursor-not-allowed opacity-70")}>
          <input id={`${name}-${o.value}`} type="radio" name={name} aria-label={o.label} className="mt-0.5 size-4 accent-[var(--brand)]" checked={value === o.value} onChange={() => onChange(o.value)} disabled={disabled} />
          <span>
            <span className="block font-medium">{o.label}</span>
            <span className="block text-xs text-muted">{o.hint}</span>
          </span>
        </label>
      ))}
    </div>
  );
}

/** Sibling discount (admin edits; bursar views). Live example on ₦150,000 of tuition. */
export function SiblingDiscountForm({ initial, canEdit }: { initial: FeeSettings; canEdit: boolean }) {
  const [saved, setSaved] = useState(initial);
  const [mode, setMode] = useState(initial.siblingMode);
  const [second, setSecond] = useState(String(initial.secondChild));
  const [third, setThird] = useState(String(initial.thirdAndLater));
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);

  const value = { siblingMode: mode, secondChild: Number(second || 0), thirdAndLater: Number(third || 0) };
  const errors = siblingErrors(value);
  const dirty = mode !== saved.siblingMode || value.secondChild !== saved.secondChild || value.thirdAndLater !== saved.thirdAndLater;
  const unit = mode === "Percentage" ? "%" : "₦";

  const save = async () => {
    setProblem(null);
    setBusy(true);
    const r = await runAction(() => saveFeeSettings(value), { success: "Sibling discount saved", onProblem: setProblem });
    setBusy(false);
    if (r) {
      setSaved(r);
      setSecond(String(r.secondChild));
      setThird(String(r.thirdAndLater));
    }
  };

  return (
    <Card className="p-5">
      <h2 className="font-display text-lg font-semibold">Sibling discount</h2>
      <p className="mb-4 text-sm text-muted">Brothers and sisters share a guardian. The eldest pays in full; the discount applies to fees marked &ldquo;Sibling discount&rdquo;.</p>
      {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
        <Alert tone="danger" title="Couldn't save" className="mb-4">
          {problemMessage(problem)}
        </Alert>
      )}
      <RadioCards name="sibling-mode" value={mode} options={modes} onChange={setMode} disabled={!canEdit} />
      {mode !== "None" && (
        <div className="mt-4 grid gap-4 sm:grid-cols-2">
          <Field id="sib-second" label={`2nd child (${unit})`} error={errors.secondChild}>
            <Input {...describedBy("sib-second", errors.secondChild)} value={second} onChange={(e) => setSecond(e.target.value)} inputMode="decimal" className="tabular" disabled={!canEdit} />
          </Field>
          <Field id="sib-third" label={`3rd child and later (${unit})`} error={errors.thirdAndLater}>
            <Input {...describedBy("sib-third", errors.thirdAndLater)} value={third} onChange={(e) => setThird(e.target.value)} inputMode="decimal" className="tabular" disabled={!canEdit} />
          </Field>
        </div>
      )}
      <p className="mt-4 rounded-input bg-surface-2 px-3 py-2 text-sm" aria-live="polite">
        {mode === "None"
          ? `Every child pays ${formatNaira(EXAMPLE_TUITION)} of ${formatNaira(EXAMPLE_TUITION)} tuition.`
          : `Example: child 2 pays ${formatNaira(siblingPays(value, 2, EXAMPLE_TUITION))} and child 3 pays ${formatNaira(siblingPays(value, 3, EXAMPLE_TUITION))} of ${formatNaira(EXAMPLE_TUITION)} tuition.`}
      </p>
      {canEdit && (
        <div className="mt-4 flex justify-end">
          <Button onClick={save} loading={busy} loadingText="Saving…" disabled={!dirty || Object.keys(errors).length > 0}>
            <Save className="size-4" aria-hidden />
            Save discount
          </Button>
        </div>
      )}
    </Card>
  );
}

type Choice = "Off" | "Term" | "Session";
const choices: { value: Choice; label: string; hint: string }[] = [
  { value: "Off", label: "Pay in full", hint: "One payment by the invoice due date." },
  { value: "Term", label: "This term", hint: "Instalments for this term only." },
  { value: "Session", label: "Whole session", hint: "The same instalments every term of the session." },
];

const toRows = (plan: InstalmentPlan): InstalmentRow[] =>
  plan.instalments.length > 0
    ? plan.instalments.map((i) => ({ percent: String(i.percent), dueOn: i.dueOn }))
    : [
        { percent: "50", dueOn: "" },
        { percent: "50", dueOn: "" },
      ];

/** Instalment plan for a term (admin edits; bursar views). A term's own plan wins over the session's. */
export function InstalmentPlanForm({ termId, termLabel, initial, canEdit }: { termId: string; termLabel: string; initial: InstalmentPlan; canEdit: boolean }) {
  const current: Choice = initial.scope === "None" || !initial.enabled ? "Off" : (initial.scope as Exclude<PlanScope, "None">);
  const [plan, setPlan] = useState(initial);
  const [choice, setChoice] = useState<Choice>(current);
  const [rows, setRows] = useState<InstalmentRow[]>(() => toRows(initial));
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  // Show row errors once a part has been edited (or Save was tried), not on a fresh form.
  const [touched, setTouched] = useState(false);

  const savedChoice: Choice = plan.scope === "None" || !plan.enabled ? "Off" : (plan.scope as Exclude<PlanScope, "None">);
  const check = instalmentErrors(rows);
  const invalid = choice !== "Off" && (Object.keys(check.rows).length > 0 || check.total !== null);
  const dirty = choice !== savedChoice || (choice !== "Off" && JSON.stringify(rows) !== JSON.stringify(toRows(plan)));
  const total = instalmentTotal(rows);

  const set = (i: number, patch: Partial<InstalmentRow>) => {
    setTouched(true);
    setRows((r) => r.map((x, j) => (j === i ? { ...x, ...patch } : x)));
  };
  const rowError = (i: number) => (touched ? check.rows[i] : undefined);

  const save = async () => {
    setProblem(null);
    setTouched(true);
    if (invalid) return;
    setBusy(true);
    const body =
      choice === "Off"
        ? // A switched-off override for this term only; other terms keep the session plan.
          { termId, scope: "Term" as const, enabled: false, instalments: [], clearSession: false }
        : { termId, scope: choice, enabled: true, instalments: rows.map((r) => ({ percent: Number(r.percent), dueOn: r.dueOn })), clearSession: false };
    const r = await runAction(() => saveInstalmentPlan(body), { success: choice === "Off" ? "Instalments switched off" : "Instalment plan saved", onProblem: setProblem });
    setBusy(false);
    if (r) {
      setPlan(r);
      setRows(toRows(r));
    }
  };

  return (
    <Card className="p-5">
      <h2 className="font-display text-lg font-semibold">Instalments</h2>
      <p className="mb-4 text-sm text-muted">
        {termLabel}:{" "}
        {savedChoice === "Off" ? "pay in full." : savedChoice === "Term" ? "this term's own plan." : "the whole-session plan."} Changes apply to invoices generated afterwards.
      </p>
      {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
        <Alert tone="danger" title="Couldn't save" className="mb-4">
          {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
        </Alert>
      )}
      <RadioCards name="plan-scope" value={choice} options={choices} onChange={setChoice} disabled={!canEdit} />
      {choice === "Off" && savedChoice === "Session" && canEdit && (
        <p className="mt-3 text-sm text-muted">Only {termLabel} is paid in full; the other terms keep the whole-session plan.</p>
      )}
      {choice !== "Off" && (
        <div className="mt-4 space-y-3">
          {rows.map((r, i) => (
            <div key={i} className="grid grid-cols-[minmax(0,1fr)_minmax(0,1.6fr)_auto] items-end gap-2 sm:grid-cols-[4.5rem_minmax(0,1fr)_minmax(0,1.4fr)_auto]">
              <p className="col-span-full text-sm font-medium sm:col-span-1 sm:pb-3">Part {i + 1}</p>
              <Field id={`inst-pct-${i}`} label="Percent">
                <Input id={`inst-pct-${i}`} value={r.percent} onChange={(e) => set(i, { percent: e.target.value })} inputMode="decimal" className="tabular" disabled={!canEdit} aria-invalid={rowError(i) ? true : undefined} />
              </Field>
              <Field id={`inst-due-${i}`} label="Due on">
                <Input id={`inst-due-${i}`} type="date" value={r.dueOn} onChange={(e) => set(i, { dueOn: e.target.value })} disabled={!canEdit} aria-invalid={rowError(i) ? true : undefined} />
              </Field>
              {canEdit && rows.length > 2 ? (
                <Button variant="ghost" size="sm" className="mb-0.5 min-h-11" onClick={() => setRows((x) => x.filter((_, j) => j !== i))} aria-label={`Remove part ${i + 1}`}>
                  <Minus className="size-4" aria-hidden />
                </Button>
              ) : (
                <span className="w-9" />
              )}
              {rowError(i) && <p className="col-span-full -mt-1 text-sm text-danger">{rowError(i)}</p>}
            </div>
          ))}
          <div className="flex flex-wrap items-center justify-between gap-2">
            {canEdit && rows.length < 3 ? (
              <Button variant="secondary" size="sm" onClick={() => setRows((x) => [...x, { percent: "", dueOn: "" }])}>
                <Plus className="size-4" aria-hidden />
                Add a part
              </Button>
            ) : (
              <span />
            )}
            <p className={cn("tabular text-sm font-medium", total === 100 ? "text-success" : "text-warning")} aria-live="polite">
              Total {total}%
            </p>
          </div>
          {touched && check.total && <p className="text-sm text-danger">{check.total}</p>}
        </div>
      )}
      {canEdit && (
        <div className="mt-4 flex flex-wrap items-center justify-end gap-2">
          {savedChoice === "Session" && (
            <ConfirmDialog
              title="Remove the plan for the whole session?"
              confirmLabel="Remove for the whole session"
              pendingLabel="Removing…"
              tone="destructive"
              trigger={
                <Button variant="ghost" className="text-danger">
                  Remove for the whole session
                </Button>
              }
              onConfirm={async () => {
                const r = await runAction(() => saveInstalmentPlan({ termId, scope: "None", enabled: false, instalments: [], clearSession: true }), {
                  success: "Whole-session plan removed",
                  onProblem: setProblem,
                });
                if (r) {
                  setPlan(r);
                  setChoice("Off");
                  setRows(toRows(r));
                }
                return r !== null;
              }}
            >
              <p>Every term of this session goes back to pay in full, unless a term has its own plan. Invoices already issued keep their schedules.</p>
            </ConfirmDialog>
          )}
          <Button onClick={save} loading={busy} loadingText="Saving…" disabled={!dirty || invalid}>
            <Save className="size-4" aria-hidden />
            Save instalments
          </Button>
        </div>
      )}
    </Card>
  );
}
