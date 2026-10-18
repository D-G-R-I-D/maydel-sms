"use client";

import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { CheckCircle2, Circle, Save, Send } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { applicationFormSchema, formValues, missingLabel, type ApplicationDetail, type ApplicationFormPayload, type ApplicationFormValues } from "@/lib/admissions/form";
import { correctApplicationForm, saveApplicationForm, submitApplication } from "@/lib/admissions/actions";
import { runAction } from "@/lib/client/action";
import { useFormDraft } from "@/lib/drafts/use-form-draft";
import { Button, Card, Field, Input, Select, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { DraftBanner } from "@/components/drafts/draft-banner";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

/** Every API field name the form can be told about (400 errors map onto these). */
const fieldNames = [
  "childFirstName",
  "childLastName",
  "childDateOfBirth",
  "childMiddleName",
  "childSex",
  "stateOfOrigin",
  "lga",
  "religion",
  "homeAddress",
  "previousSchool",
  "previousClass",
  "medicalNotes",
  "specialNeeds",
  "fatherName",
  "fatherPhone",
  "fatherEmail",
  "fatherOccupation",
  "motherName",
  "motherPhone",
  "motherEmail",
  "motherOccupation",
  "emergencyContactName",
  "emergencyContactPhone",
  "declaration",
] as const;
type FieldName = (typeof fieldNames)[number];
const top = new Set(["childFirstName", "childLastName", "childDateOfBirth"]);
const pathOf = (f: FieldName) => (top.has(f) ? f : `form.${f}`) as Parameters<ReturnType<typeof useForm<ApplicationFormValues>>["setError"]>[0];

/**
 * E11.5: the application form. `family` mode saves drafts (kept locally across reloads too) and submits
 * with a confirm; `correction` mode is the admin's audited edit of a submitted form.
 */
export function ApplicationFormEditor({ detail, mode, onSaved }: { detail: ApplicationDetail; mode: "family" | "correction"; onSaved?: (d: ApplicationDetail) => void }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const [missing, setMissing] = useState(detail.missingToSubmit);
  const [confirmSubmit, setConfirmSubmit] = useState(false);
  const form = useForm<ApplicationFormValues, unknown, ApplicationFormPayload>({ resolver: zodResolver(applicationFormSchema), defaultValues: formValues(detail) });
  const {
    register,
    handleSubmit,
    setError,
    reset,
    control,
    formState: { errors, isSubmitting, isDirty },
  } = form;
  const draft = useFormDraft(form, { scope: ["application-form", mode, detail.application.id] });
  const declaration = useWatch({ control, name: "form.declaration" });
  const id = detail.application.id;
  const e = (f: FieldName): string | undefined => {
    const err = top.has(f) ? (errors as Record<string, { message?: string } | undefined>)[f] : (errors.form as Record<string, { message?: string } | undefined> | undefined)?.[f];
    return err?.message;
  };

  const applyServerErrors = (p: Problem) => {
    if (p.status !== 400 || !p.errors) return false;
    let applied = false;
    for (const [key, msgs] of Object.entries(p.errors)) {
      const name = key.replace(/^form\./i, "") as FieldName;
      if ((fieldNames as readonly string[]).includes(name)) {
        setError(pathOf(name), { type: "server", message: msgs.join(" ") }, { shouldFocus: !applied });
        applied = true;
      }
    }
    return applied;
  };

  const save = handleSubmit(async (values) => {
    setProblem(null);
    const r = await runAction(() => (mode === "family" ? saveApplicationForm(id, values) : correctApplicationForm(id, values)), {
      success: mode === "family" ? "Draft saved" : "Correction saved",
      onProblem: (p) => (applyServerErrors(p) ? undefined : setProblem(p)),
    });
    if (r) {
      draft.clear();
      reset(formValues(r));
      setMissing(r.missingToSubmit);
      onSaved?.(r);
    }
  });

  const text = (name: FieldName, label: string, opts: { type?: string; placeholder?: string; className?: string; autoComplete?: string } = {}) => (
    <Field id={`af-${name}`} label={label} error={e(name)} className={opts.className}>
      <Input {...describedBy(`af-${name}`, e(name))} {...register(top.has(name) ? (name as "childFirstName") : (`form.${name}` as "form.lga"))} type={opts.type} placeholder={opts.placeholder} autoComplete={opts.autoComplete ?? "off"} />
    </Field>
  );
  const area = (name: FieldName, label: string) => (
    <Field id={`af-${name}`} label={label} error={e(name)} className="sm:col-span-2">
      <textarea {...describedBy(`af-${name}`, e(name))} {...register(`form.${name}` as "form.medicalNotes")} rows={3} className={textareaClass} />
    </Field>
  );
  const section = (title: string, children: React.ReactNode, hint?: string) => (
    <Card className="p-5">
      <h2 className="font-display text-lg font-semibold">{title}</h2>
      {hint && <p className="mt-0.5 text-sm text-muted">{hint}</p>}
      <div className="mt-4 grid gap-4 sm:grid-cols-2">{children}</div>
    </Card>
  );

  return (
    <form onSubmit={save} noValidate className="space-y-5">
      <DraftBanner draft={draft} onDiscard={draft.discard} />
      {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
        <Alert tone="danger" title={problem.code === "admission.form_locked" ? "This form has been submitted" : "Couldn't save"}>
          {problemMessage(problem)}
        </Alert>
      )}

      {section(
        "Child",
        <>
          {text("childFirstName", "First name")}
          {text("childMiddleName", "Middle name (optional)")}
          {text("childLastName", "Surname")}
          {text("childDateOfBirth", "Date of birth", { type: "date" })}
          <Field id="af-childSex" label="Sex" error={e("childSex")}>
            <Select {...describedBy("af-childSex", e("childSex"))} {...register("form.childSex")}>
              <option value="">Choose</option>
              <option value="Female">Female</option>
              <option value="Male">Male</option>
            </Select>
          </Field>
          {text("religion", "Religion (optional)")}
          {text("stateOfOrigin", "State of origin (optional)")}
          {text("lga", "LGA (optional)")}
          <Field id="af-homeAddress" label="Home address" error={e("homeAddress")} className="sm:col-span-2">
            <textarea {...describedBy("af-homeAddress", e("homeAddress"))} {...register("form.homeAddress")} rows={2} autoComplete="street-address" className={textareaClass} />
          </Field>
          {text("previousSchool", "Previous school (optional)")}
          {text("previousClass", "Class at previous school (optional)")}
        </>,
      )}
      {section(
        "Father / guardian",
        <>
          {text("fatherName", "Full name", { autoComplete: "name" })}
          {text("fatherPhone", "Phone", { type: "tel", placeholder: "+234 803 123 4567" })}
          {text("fatherEmail", "Email (optional)", { type: "email" })}
          {text("fatherOccupation", "Occupation (optional)")}
        </>,
        "Give at least one parent's name and phone number.",
      )}
      {section(
        "Mother / guardian",
        <>
          {text("motherName", "Full name")}
          {text("motherPhone", "Phone", { type: "tel", placeholder: "+234 803 123 4567" })}
          {text("motherEmail", "Email (optional)", { type: "email" })}
          {text("motherOccupation", "Occupation (optional)")}
        </>,
      )}
      {section(
        "Emergency contact (optional)",
        <>
          {text("emergencyContactName", "Name")}
          {text("emergencyContactPhone", "Phone", { type: "tel" })}
        </>,
      )}
      {section(
        "Medical",
        <>
          {area("medicalNotes", "Allergies, conditions or medication (optional)")}
          {area("specialNeeds", "Special educational needs (optional)")}
        </>,
      )}

      <Card className="p-5">
        <h2 className="font-display text-lg font-semibold">Declaration</h2>
        <label htmlFor="af-declaration" className="mt-3 flex cursor-pointer items-start gap-2.5 text-sm">
          <input id="af-declaration" type="checkbox" className="mt-0.5 size-4 shrink-0 accent-[var(--brand)]" {...register("form.declaration")} />
          <span>I confirm that the information given in this form is true and complete to the best of my knowledge.</span>
        </label>
        {e("declaration") && <p className="mt-1 text-sm text-danger">{e("declaration")}</p>}
      </Card>

      <div className="sticky bottom-0 z-10 -mx-4 border-t border-border bg-surface/95 px-4 py-3 backdrop-blur sm:mx-0 sm:rounded-card sm:border sm:shadow-low">
        {mode === "family" && (
          <ul className="mb-3 grid gap-1 text-sm sm:grid-cols-2" aria-label="Still needed to submit">
            {missing.length === 0 ? (
              <li className="flex items-center gap-2 text-success">
                <CheckCircle2 className="size-4" aria-hidden />
                Everything needed is here. You can submit.
              </li>
            ) : (
              [...new Set(missing.map((m) => missingLabel[m] ?? m))].map((m) => (
                <li key={m} className="flex items-center gap-2 text-muted">
                  <Circle className="size-3.5" aria-hidden />
                  {m}
                </li>
              ))
            )}
          </ul>
        )}
        <div className="flex flex-wrap items-center justify-end gap-2">
          <span className="mr-auto text-xs text-muted">{isDirty ? "Unsaved changes" : mode === "family" ? "Saved" : ""}</span>
          <Button type="submit" variant={mode === "family" ? "secondary" : "primary"} loading={isSubmitting} loadingText="Saving…" disabled={!isDirty}>
            <Save className="size-4" aria-hidden />
            {mode === "family" ? "Save draft" : "Save correction"}
          </Button>
          {mode === "family" && (
            <Button type="button" disabled={isDirty || missing.length > 0 || !declaration} onClick={() => setConfirmSubmit(true)} title={isDirty ? "Save your changes first" : undefined}>
              <Send className="size-4" aria-hidden />
              Submit application
            </Button>
          )}
        </div>
      </div>

      {mode === "family" && (
        <ConfirmDialog
          open={confirmSubmit}
          onOpenChange={setConfirmSubmit}
          title="Submit the application?"
          confirmLabel="Submit"
          pendingLabel="Submitting…"
          onConfirm={async () => {
            const r = await runAction(() => submitApplication(id), {
              success: "Application submitted",
              onProblem: (p) => (applyServerErrors(p) ? undefined : setProblem(p)),
            });
            if (r) onSaved?.(r);
            return r !== null;
          }}
        >
          <p>You can&apos;t change the form or documents after submitting. We&apos;ll email you a confirmation and the next step: booking the entrance exam.</p>
        </ConfirmDialog>
      )}
    </form>
  );
}
