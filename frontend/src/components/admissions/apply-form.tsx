"use client";

import Link from "next/link";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { AdmissionsInfo, MeResponse } from "@/lib/api/types";
import { registerSchema, type RegisterFormValues, type RegisterPayload } from "@/lib/admissions/rules";
import { formatNaira } from "@/lib/finance/rules";
import { applyFieldErrors, postJson } from "@/lib/client/bff";
import { Button, Field, Input, Select, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { PasswordInput } from "@/components/ui/password-input";

const fields = ["parentName", "email", "phone", "password", "childFirstName", "childLastName", "childDateOfBirth", "classLevelId", "sessionId"] as const;

/** E11.4: buy an admission form. Creates the family's account and signs it in (BFF /api/auth/register). */
export function ApplyForm({ sessions }: { sessions: AdmissionsInfo[] }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const [emailInUse, setEmailInUse] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues, unknown, RegisterPayload>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      parentName: "",
      email: "",
      phone: "",
      password: "",
      childFirstName: "",
      childLastName: "",
      childDateOfBirth: "",
      classLevelId: "",
      sessionId: sessions[0]?.sessionId ?? "",
      website: "",
    },
  });
  const sessionId = useWatch({ control, name: "sessionId" });
  const session = sessions.find((s) => s.sessionId === sessionId) ?? sessions[0];

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    setEmailInUse(false);
    const result = await postJson<{ user: MeResponse; redirectTo: string }>("/api/auth/register", values);
    if (result.ok) {
      window.location.assign(result.data.redirectTo);
      return;
    }
    const p = result.problem;
    if (p.code === "admission.email_in_use") {
      setEmailInUse(true);
      return;
    }
    if (applyFieldErrors(p, fields, setError)) return;
    setProblem(p);
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-6">
      {emailInUse && (
        <Alert tone="info" title="You already have an account">
          Sign in to apply from your account, including for another child.{" "}
          <Link href="/login?next=%2Fadmissions" className="font-medium text-brand hover:underline">
            Sign in
          </Link>
        </Alert>
      )}
      {problem && (
        <Alert tone="danger" title={problem.code === "admission.closed" ? "Admissions are closed" : "Couldn't create your account"}>
          {problemMessage(problem)}
        </Alert>
      )}

      {/* Honeypot: hidden from people and assistive tech; bots fill it in and the API ignores them. */}
      <div className="absolute -left-[9999px] h-px w-px overflow-hidden" aria-hidden="true">
        <label htmlFor="apply-website">Website (leave empty)</label>
        <input id="apply-website" type="text" tabIndex={-1} autoComplete="off" {...register("website")} />
      </div>

      <section className="space-y-4">
        <h2 className="font-display text-lg font-semibold">About your child</h2>
        {sessions.length > 1 && (
          <Field id="apply-session" label="Session" error={errors.sessionId?.message}>
            <Select {...describedBy("apply-session", errors.sessionId?.message)} {...register("sessionId")}>
              {sessions.map((s) => (
                <option key={s.sessionId} value={s.sessionId}>
                  {s.session}
                </option>
              ))}
            </Select>
          </Field>
        )}
        <div className="grid gap-4 sm:grid-cols-2">
          <Field id="apply-child-first" label="First name" error={errors.childFirstName?.message}>
            <Input {...describedBy("apply-child-first", errors.childFirstName?.message)} {...register("childFirstName")} autoComplete="off" />
          </Field>
          <Field id="apply-child-last" label="Surname" error={errors.childLastName?.message}>
            <Input {...describedBy("apply-child-last", errors.childLastName?.message)} {...register("childLastName")} autoComplete="off" />
          </Field>
          <Field id="apply-child-dob" label="Date of birth (optional)" error={errors.childDateOfBirth?.message}>
            <Input {...describedBy("apply-child-dob", errors.childDateOfBirth?.message)} {...register("childDateOfBirth")} type="date" />
          </Field>
          <Field id="apply-class" label="Class applying for" error={errors.classLevelId?.message}>
            <Select {...describedBy("apply-class", errors.classLevelId?.message)} {...register("classLevelId")}>
              <option value="">Choose a class</option>
              {(session?.classLevels ?? []).map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name}
                </option>
              ))}
            </Select>
          </Field>
        </div>
      </section>

      <section className="space-y-4">
        <h2 className="font-display text-lg font-semibold">Your account</h2>
        <p className="-mt-2 text-sm text-muted">You&apos;ll use this to fill in the form, book the entrance exam and see the outcome. If your child is admitted, it becomes your parent portal login.</p>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field id="apply-parent" label="Your full name" error={errors.parentName?.message} className="sm:col-span-2">
            <Input {...describedBy("apply-parent", errors.parentName?.message)} {...register("parentName")} autoComplete="name" />
          </Field>
          <Field id="apply-email" label="Email" error={errors.email?.message}>
            <Input {...describedBy("apply-email", errors.email?.message)} {...register("email")} type="email" autoComplete="email" spellCheck={false} />
          </Field>
          <Field id="apply-phone" label="Phone" error={errors.phone?.message}>
            <Input {...describedBy("apply-phone", errors.phone?.message)} {...register("phone")} type="tel" inputMode="tel" autoComplete="tel" placeholder="+234 803 123 4567" />
          </Field>
          <Field id="apply-password" label="Password" error={errors.password?.message} hint="At least 10 characters with upper- and lower-case letters and a number." className="sm:col-span-2">
            <PasswordInput {...describedBy("apply-password", errors.password?.message, true)} {...register("password")} autoComplete="new-password" />
          </Field>
        </div>
      </section>

      <div className="flex flex-col gap-3 border-t border-border pt-4 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-muted">
          {session && session.formFee > 0 ? (
            <>
              Form fee <span className="tabular font-semibold text-text">{formatNaira(session.formFee)}</span>. You&apos;ll pay online or at the school next.
            </>
          ) : (
            "There's no form fee this session."
          )}
        </p>
        <Button type="submit" size="lg" loading={isSubmitting} loadingText="Creating your account…">
          Create account and continue
        </Button>
      </div>
    </form>
  );
}
