"use client";

import { useFormDraft } from "@/lib/drafts/use-form-draft";
import { DraftBanner } from "@/components/drafts/draft-banner";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { SchoolProfile } from "@/lib/api/types";
import { runAction } from "@/lib/client/action";
import { updateSchoolProfile } from "@/lib/school/actions";
import { schoolProfileSchema, type SchoolProfileFormValues, type SchoolProfilePayload } from "@/lib/validation/school";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

const fields = ["name", "motto", "address", "phone", "email", "website", "principalName", "whatsApp", "mapUrl", "facebookUrl", "instagramUrl", "officeHours"] as const;

const toValues = (p: SchoolProfile): SchoolProfileFormValues => ({
  name: p.name,
  motto: p.motto ?? "",
  address: p.address,
  phone: p.phone ?? "",
  email: p.email ?? "",
  website: p.website ?? "",
  principalName: p.principalName ?? "",
  whatsApp: p.whatsApp ?? "",
  mapUrl: p.mapUrl ?? "",
  facebookUrl: p.facebookUrl ?? "",
  instagramUrl: p.instagramUrl ?? "",
  officeHours: p.officeHours ?? "",
});

/** The school's details for report cards (E2.6) and the public website (E7). One PUT replaces every text field. */
export function SchoolProfileForm({ profile }: { profile: SchoolProfile }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const form = useForm<SchoolProfileFormValues, unknown, SchoolProfilePayload>({
    resolver: zodResolver(schoolProfileSchema),
    defaultValues: toValues(profile),
  });
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = form;
  const draft = useFormDraft(form, { scope: ["school-profile"] });

  const onSubmit = handleSubmit(async (body) => {
    setProblem(null);
    const saved = await runAction(() => updateSchoolProfile(body), { success: "School profile saved", fields, setError, onProblem: setProblem });
    if (saved) {
      draft.clear();
      reset(toValues(saved));
    }
  });

  const err = (f: (typeof fields)[number]) => errors[f]?.message;

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      <DraftBanner draft={draft} onDiscard={draft.discard} />
      {problem && problem.status > 0 && problem.status !== 429 && problem.status < 500 && (
        <Alert tone="danger" className="animate-rise" title="Couldn't save">
          <p>{problemMessage(problem)}</p>
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="school-name" label="School name" error={err("name")} className="sm:col-span-2">
          <Input {...describedBy("school-name", err("name"))} {...register("name")} autoComplete="organization" />
        </Field>
        <Field id="school-motto" label="Motto (optional)" error={err("motto")} className="sm:col-span-2">
          <Input {...describedBy("school-motto", err("motto"))} {...register("motto")} autoComplete="off" placeholder="e.g. The Well Spring of Knowledge" />
        </Field>
        <Field id="school-address" label="Address" error={err("address")} className="sm:col-span-2">
          <textarea {...describedBy("school-address", err("address"))} {...register("address")} rows={3} autoComplete="street-address" className="block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm" />
        </Field>
        <Field id="school-phone" label="Phone (optional)" error={err("phone")}>
          <Input {...describedBy("school-phone", err("phone"))} {...register("phone")} type="tel" inputMode="tel" autoComplete="off" placeholder="+234 803 123 4567" />
        </Field>
        <Field id="school-email" label="Email (optional)" error={err("email")}>
          <Input {...describedBy("school-email", err("email"))} {...register("email")} type="email" autoComplete="off" spellCheck={false} />
        </Field>
        <Field id="school-website" label="Website (optional)" error={err("website")}>
          <Input {...describedBy("school-website", err("website"))} {...register("website")} inputMode="url" autoComplete="off" spellCheck={false} placeholder="e.g. maydelschools.com" />
        </Field>
        <Field id="school-principal" label="Principal's name (optional)" error={err("principalName")} hint="Printed under the signature on report cards.">
          <Input {...describedBy("school-principal", err("principalName"), true)} {...register("principalName")} autoComplete="off" />
        </Field>
      </div>
      <fieldset className="space-y-4 border-t border-border pt-4">
        <legend className="text-sm font-semibold">On the website</legend>
        <p className="-mt-2 text-xs text-muted">Shown in the website's footer and contact page. Leave any blank to hide it.</p>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field id="school-whatsapp" label="WhatsApp number (optional)" error={err("whatsApp")} hint="Visitors get a “Chat on WhatsApp” button.">
            <Input {...describedBy("school-whatsapp", err("whatsApp"), true)} {...register("whatsApp")} type="tel" inputMode="tel" autoComplete="off" placeholder="0803 123 4567" />
          </Field>
          <Field id="school-hours" label="Office hours (optional)" error={err("officeHours")}>
            <Input {...describedBy("school-hours", err("officeHours"))} {...register("officeHours")} autoComplete="off" placeholder="Mon–Fri, 7:30 am – 4:00 pm" />
          </Field>
          <Field id="school-map" label="Google Maps link (optional)" error={err("mapUrl")} hint="In Google Maps, Share → Copy link." className="sm:col-span-2">
            <Input {...describedBy("school-map", err("mapUrl"), true)} {...register("mapUrl")} inputMode="url" autoComplete="off" spellCheck={false} placeholder="https://maps.app.goo.gl/…" />
          </Field>
          <Field id="school-facebook" label="Facebook page (optional)" error={err("facebookUrl")}>
            <Input {...describedBy("school-facebook", err("facebookUrl"))} {...register("facebookUrl")} inputMode="url" autoComplete="off" spellCheck={false} placeholder="https://facebook.com/…" />
          </Field>
          <Field id="school-instagram" label="Instagram (optional)" error={err("instagramUrl")}>
            <Input {...describedBy("school-instagram", err("instagramUrl"))} {...register("instagramUrl")} inputMode="url" autoComplete="off" spellCheck={false} placeholder="https://instagram.com/…" />
          </Field>
        </div>
      </fieldset>
      <div className="flex justify-end">
        <Button type="submit" loading={isSubmitting} loadingText="Saving…" disabled={!isDirty}>
          Save details
        </Button>
      </div>
    </form>
  );
}
