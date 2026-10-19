"use client";

import { useState } from "react";
import { Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { AdmissionSettings } from "@/lib/api/types";
import { saveAdmissionSettings } from "@/lib/admissions/actions";
import { formatNaira, parseAmount } from "@/lib/finance/rules";
import { runAction } from "@/lib/client/action";
import { Button, Card, Field, Input, Select, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";

/** E11.4: per-session admissions settings. Admin edits; bursar sees them read-only. */
export function AdmissionSettingsForm({ initial, canEdit, terms = [] }: { initial: AdmissionSettings; canEdit: boolean; terms?: { id: string; label: string }[] }) {
  const [saved, setSaved] = useState(initial);
  const [entryTermId, setEntryTermId] = useState(initial.entryTermId ?? "");
  const [isOpen, setIsOpen] = useState(initial.isOpen);
  const [fee, setFee] = useState(initial.formFee.toFixed(2));
  const [days, setDays] = useState(String(initial.examWindowDays));
  const [instructions, setInstructions] = useState(initial.instructions ?? "");
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);

  const feeValue = parseAmount(fee);
  const daysValue = Number(days);
  const feeError = feeValue === null ? "Enter ₦0 or more, with at most 2 decimal places." : undefined;
  const daysError = !Number.isInteger(daysValue) || daysValue < 1 || daysValue > 30 ? "Enter a whole number of days from 1 to 30." : undefined;
  const instructionsError = instructions.length > 2000 ? "Use 2,000 characters or fewer." : undefined;
  const dirty = isOpen !== saved.isOpen || feeValue !== saved.formFee || daysValue !== saved.examWindowDays || (instructions.trim() || null) !== (saved.instructions ?? null) || (entryTermId || null) !== (saved.entryTermId ?? null);

  const save = async () => {
    setProblem(null);
    if (feeError || daysError || instructionsError) return;
    setBusy(true);
    const r = await runAction(
      () => saveAdmissionSettings({ sessionId: saved.sessionId, isOpen, formFee: feeValue!, examWindowDays: daysValue, instructions: instructions.trim() || null, entryTermId: entryTermId || null }),
      { success: isOpen ? "Admissions settings saved: open" : "Admissions settings saved: closed", onProblem: setProblem },
    );
    setBusy(false);
    if (r) setSaved(r);
  };

  return (
    <Card className="space-y-5 p-5">
      {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
        <Alert tone="danger" title="Couldn't save">
          {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
        </Alert>
      )}
      <div className="flex items-start justify-between gap-4 rounded-input border border-border p-3">
        <div>
          <p className="text-sm font-medium">{isOpen ? "Open: families can buy the form" : "Closed: the apply page says admissions are closed"}</p>
          <p className="text-xs text-muted">Shown on the public “Buy admission form” page for {saved.session}.</p>
        </div>
        <Switch checked={isOpen} onCheckedChange={setIsOpen} label="Admissions open" disabled={!canEdit} />
      </div>
      {terms.length > 1 && (
        <Field
          id="adm-entry"
          label="New pupils start in"
          hint={saved.entryStartsOn ? `Offer letters give the resumption date; enrolment bills new-intake fees from this term.` : "Offer letters and enrolment use this term's start; leave it on the first term for a normal intake."}
        >
          <Select {...describedBy("adm-entry", undefined, true)} value={entryTermId} onChange={(e) => setEntryTermId(e.target.value)} disabled={!canEdit}>
            <option value="">{terms[0]?.label ?? "First term"} (the normal intake)</option>
            {terms.slice(1).map((t) => (
              <option key={t.id} value={t.id}>
                {t.label}
              </option>
            ))}
          </Select>
        </Field>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="adm-fee" label="Form fee (₦)" error={feeError} hint={feeValue === 0 ? "Free: families skip the payment step." : feeValue ? formatNaira(feeValue) : undefined}>
          <Input {...describedBy("adm-fee", feeError, true)} value={fee} onChange={(e) => setFee(e.target.value)} inputMode="decimal" className="tabular" disabled={!canEdit} />
        </Field>
        <Field id="adm-days" label="Days to book the exam after paying" error={daysError}>
          <Input {...describedBy("adm-days", daysError)} value={days} onChange={(e) => setDays(e.target.value)} inputMode="numeric" className="tabular" disabled={!canEdit} />
        </Field>
      </div>
      <Field id="adm-instructions" label="Instructions for families (optional)" error={instructionsError} hint="Shown on the apply page, e.g. what to bring on exam day.">
        <textarea
          {...describedBy("adm-instructions", instructionsError, true)}
          value={instructions}
          onChange={(e) => setInstructions(e.target.value)}
          rows={5}
          disabled={!canEdit}
          className="block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand disabled:opacity-70 sm:text-sm"
        />
      </Field>
      {canEdit && (
        <div className="flex justify-end">
          <Button onClick={save} loading={busy} loadingText="Saving…" disabled={!dirty || Boolean(feeError || daysError || instructionsError)}>
            <Save className="size-4" aria-hidden />
            Save settings
          </Button>
        </div>
      )}
    </Card>
  );
}
