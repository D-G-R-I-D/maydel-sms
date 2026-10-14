"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { GuardianDetail } from "@/lib/api/types";
import { updateGuardian } from "@/lib/students/actions";
import { runAction } from "@/lib/client/action";
import { guardianDetailsSchema, type GuardianDetailsPayload, type GuardianDetailsValues } from "@/lib/validation/students";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

const fields = ["fullName", "phone", "email", "occupation", "address"] as const;
const conflictField: Record<string, (typeof fields)[number]> = { "guardian.phone_in_use": "phone", "guardian.login_in_use": "email" };

/** Edit a guardian's own details (shared by all their children). */
export function GuardianForm({ guardian }: { guardian: GuardianDetail }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<GuardianDetailsValues, unknown, GuardianDetailsPayload>({
    resolver: zodResolver(guardianDetailsSchema),
    defaultValues: { fullName: guardian.fullName, phone: guardian.phone, email: guardian.email ?? "", occupation: guardian.occupation ?? "", address: guardian.address ?? "" },
  });

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    const saved = await runAction(() => updateGuardian(guardian.id, values), {
      success: (g) => `${g.fullName} saved`,
      fields,
      setError,
      onProblem: (p) => {
        const f = p.code ? conflictField[p.code] : undefined;
        if (f) setError(f, { type: "server", message: problemMessage(p) }, { shouldFocus: true });
        else setProblem(p);
      },
    });
    if (saved) reset({ fullName: saved.fullName, phone: saved.phone, email: saved.email ?? "", occupation: saved.occupation ?? "", address: saved.address ?? "" });
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {problem && problem.status > 0 && problem.status !== 429 && problem.status < 500 && (
        <Alert tone="danger" className="animate-rise" title="Couldn't save">
          <p>{problemMessage(problem)}</p>
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="gd-name" label="Full name" error={errors.fullName?.message} className="sm:col-span-2">
          <Input {...describedBy("gd-name", errors.fullName?.message)} {...register("fullName")} autoComplete="off" />
        </Field>
        <Field id="gd-phone" label="Phone" error={errors.phone?.message} hint="Saved as +234…">
          <Input {...describedBy("gd-phone", errors.phone?.message, true)} {...register("phone")} type="tel" inputMode="tel" autoComplete="off" />
        </Field>
        <Field id="gd-email" label="Email (optional)" error={errors.email?.message}>
          <Input {...describedBy("gd-email", errors.email?.message)} {...register("email")} type="email" autoComplete="off" spellCheck={false} />
        </Field>
        <Field id="gd-occupation" label="Occupation (optional)" error={errors.occupation?.message}>
          <Input {...describedBy("gd-occupation", errors.occupation?.message)} {...register("occupation")} autoComplete="off" />
        </Field>
        <Field id="gd-address" label="Address (optional)" error={errors.address?.message}>
          <Input {...describedBy("gd-address", errors.address?.message)} {...register("address")} autoComplete="off" />
        </Field>
      </div>
      <div className="flex justify-end">
        <Button type="submit" loading={isSubmitting} loadingText="Saving…" disabled={!isDirty}>
          Save changes
        </Button>
      </div>
    </form>
  );
}
