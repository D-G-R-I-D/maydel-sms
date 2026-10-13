"use client";

import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Check, Circle } from "lucide-react";
import { ProblemCodes, problemMessage, type Problem } from "@/lib/api/problem";
import type { MeResponse } from "@/lib/api/types";
import { applyFieldErrors, postJson } from "@/lib/client/bff";
import { changePasswordSchema, passwordChecklist, passwordStrength, type ChangePasswordInput } from "@/lib/validation/auth";
import { Button, Field, cn, describedBy } from "@/components/ui/primitives";
import { PasswordInput } from "@/components/ui/password-input";
import { Alert, TraceId } from "@/components/ui/states";
import { toast } from "@/components/ui/toast";

const fields = ["currentPassword", "newPassword", "confirmPassword"] as const;

const strengthStyles = [
  { width: "w-0", bar: "bg-border", text: "text-muted" },
  { width: "w-1/4", bar: "bg-danger", text: "text-danger" },
  { width: "w-2/4", bar: "bg-warning", text: "text-warning" },
  { width: "w-3/4", bar: "bg-brand", text: "text-brand" },
  { width: "w-full", bar: "bg-success", text: "text-success" },
] as const;

function StrengthMeter({ password }: { password: string }) {
  const { score, label } = passwordStrength(password);
  const style = strengthStyles[score];
  return (
    <div className="mt-2" aria-live="polite">
      <div className="h-1.5 overflow-hidden rounded-full bg-surface-2">
        <div className={cn("h-full rounded-full transition-all duration-250 ease-out", style.width, style.bar)} />
      </div>
      {password && <p className={cn("mt-1 text-xs font-medium", style.text)}>Strength: {label}</p>}
    </div>
  );
}

export function ChangePasswordForm() {
  const [problem, setProblem] = useState<Problem | null>(null);
  const [done, setDone] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = useForm<ChangePasswordInput>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });
  const newPassword = useWatch({ control, name: "newPassword" }) ?? "";
  const confirmPassword = useWatch({ control, name: "confirmPassword" }) ?? "";
  const matches = confirmPassword.length > 0 && confirmPassword === newPassword;

  const onSubmit = handleSubmit(async ({ currentPassword, newPassword }) => {
    setProblem(null);
    const result = await postJson<{ user: MeResponse; redirectTo: string }>("/api/auth/change-password", { currentPassword, newPassword });
    if (result.ok) {
      setDone(true);
      window.location.assign(result.data.redirectTo);
      return;
    }
    const p = result.problem;
    if (p.code === ProblemCodes.invalidCredentials) {
      setError("currentPassword", { type: "server", message: problemMessage(p) }, { shouldFocus: true });
      return;
    }
    if (p.status === 401) {
      window.location.assign("/login?reason=expired&next=%2Fchange-password");
      return;
    }
    if (p.status === 429) {
      toast.warning("Too many attempts", { description: problemMessage(p) });
      return;
    }
    if (p.status === 0 || p.status >= 500) {
      toast.error(problemMessage(p), { description: p.traceId ? `Reference: ${p.traceId}` : undefined });
      return;
    }
    if (!applyFieldErrors(p, fields, setError)) setProblem(p);
  });

  if (done) {
    return (
      <Alert tone="success" title="Password changed" className="animate-rise">
        Taking you to your dashboard…
      </Alert>
    );
  }

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {problem && (
        <Alert tone="danger" title="Couldn't change your password" className="animate-rise">
          <p>{problemMessage(problem)}</p>
          <TraceId traceId={problem.traceId} />
        </Alert>
      )}
      <Field id="currentPassword" label="Current password" error={errors.currentPassword?.message}>
        <PasswordInput {...describedBy("currentPassword", errors.currentPassword?.message)} {...register("currentPassword")} autoComplete="current-password" />
      </Field>
      <Field
        id="newPassword"
        label="New password"
        error={errors.newPassword?.message}
        hint={
          <ul className="mt-1 grid grid-cols-1 gap-1 sm:grid-cols-2" aria-label="Password requirements">
            {passwordChecklist.map((rule) => {
              const met = rule.test(newPassword);
              return (
                <li key={rule.label} className={cn("flex items-center gap-1.5 transition-colors duration-200", met ? "text-success" : "text-muted")}>
                  {met ? <Check className="animate-fade size-3.5" aria-hidden /> : <Circle className="size-3.5" aria-hidden />}
                  {rule.label}
                  <span className="sr-only">{met ? "(done)" : "(not yet)"}</span>
                </li>
              );
            })}
          </ul>
        }
      >
        <PasswordInput {...describedBy("newPassword", errors.newPassword?.message, true)} {...register("newPassword")} autoComplete="new-password" />
        <StrengthMeter password={newPassword} />
      </Field>
      <Field id="confirmPassword" label="Confirm new password" error={errors.confirmPassword?.message}>
        <PasswordInput {...describedBy("confirmPassword", errors.confirmPassword?.message)} {...register("confirmPassword")} autoComplete="new-password" />
        {matches && !errors.confirmPassword && (
          <p className="animate-fade mt-1.5 flex items-center gap-1.5 text-xs font-medium text-success">
            <Check className="size-3.5" aria-hidden />
            Passwords match
          </p>
        )}
      </Field>
      <Button type="submit" size="lg" className="w-full" loading={isSubmitting}>
        Change password
      </Button>
    </form>
  );
}
