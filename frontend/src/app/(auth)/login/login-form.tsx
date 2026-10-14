"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { FlaskConical } from "lucide-react";
import { ProblemCodes, problemMessage, type Problem } from "@/lib/api/problem";
import type { MeResponse } from "@/lib/api/types";
import { applyFieldErrors, postJson } from "@/lib/client/bff";
import { loginSchema, type LoginInput } from "@/lib/validation/auth";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { PasswordInput } from "@/components/ui/password-input";
import { Alert, TraceId } from "@/components/ui/states";
import { toast } from "@/components/ui/toast";
import { GoogleButton } from "./google-button";

type SignedIn = { user: MeResponse; redirectTo: string };

/** Seeded in Development only (CLAUDE.md). Shown when the server says demo accounts are on. */
const demoAccounts = [
  { label: "Admin", userName: "admin@maydel.test" },
  { label: "Teacher", userName: "teacher@maydel.test" },
  { label: "Bursar", userName: "bursar@maydel.test" },
  { label: "Parent", userName: "parent@maydel.test" },
  { label: "Student", userName: "MSS/2026/0001" },
  { label: "New teacher", userName: "newteacher@maydel.test" },
];
const DEMO_PASSWORD = "Demo#Pass2026";

export function LoginForm({ next, googleClientId, showDemo }: { next: string | null; googleClientId: string; showDemo: boolean }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const [googleHidden, setGoogleHidden] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<LoginInput>({ resolver: zodResolver(loginSchema), defaultValues: { userName: "", password: "" } });

  const finish = (result: Awaited<ReturnType<typeof postJson<SignedIn>>>) => {
    if (result.ok) {
      setRedirecting(true);
      window.location.assign(result.data.redirectTo);
      return;
    }
    const p = result.problem;
    if (p.code === ProblemCodes.googleDisabled) {
      setGoogleHidden(true);
      toast.info("Google sign-in isn't available right now", { description: "Use your email and password instead." });
      return;
    }
    // Transient trouble is a toast; anything about the credentials stays on the form.
    if (p.status === 0 || p.status >= 500) {
      toast.error(problemMessage(p), { description: p.traceId ? `Reference: ${p.traceId}` : undefined });
      return;
    }
    if (p.status === 429) {
      toast.warning("Too many attempts", { description: problemMessage(p) });
      return;
    }
    if (!applyFieldErrors(p, ["userName", "password"] as const, setError)) {
      setProblem(p);
      setFocus("password");
    }
  };

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    finish(await postJson<SignedIn>("/api/auth/login", { ...values, next }));
  });

  const onGoogleCredential = async (credential: string) => {
    setProblem(null);
    finish(await postJson<SignedIn>("/api/auth/google", { credential, next }));
  };

  const fillDemo = (userName: string) => {
    setProblem(null);
    setValue("userName", userName, { shouldValidate: true });
    setValue("password", DEMO_PASSWORD, { shouldValidate: true });
    setFocus("password");
  };

  const busy = isSubmitting || redirecting;

  return (
    <div className="space-y-6">
      {problem && (
        <Alert tone="danger" title="Couldn't sign you in" className="animate-rise">
          <p>{problemMessage(problem)}</p>
          <TraceId traceId={problem.status >= 500 ? problem.traceId : undefined} />
        </Alert>
      )}

      <form method="post" onSubmit={onSubmit} noValidate className="space-y-4">
        <Field id="userName" label="Email or admission number" error={errors.userName?.message}>
          <Input
            {...describedBy("userName", errors.userName?.message)}
            {...register("userName")}
            autoComplete="username"
            autoCapitalize="none"
            spellCheck={false}
            placeholder="e.g. MSS/2026/0001"
          />
        </Field>
        <Field id="password" label="Password" error={errors.password?.message}>
          <PasswordInput {...describedBy("password", errors.password?.message)} {...register("password")} autoComplete="current-password" />
        </Field>
        <Button type="submit" size="lg" className="w-full" loading={busy} loadingText="Signing in…">
          {redirecting ? "Signing you in…" : "Sign in"}
        </Button>
      </form>

      {googleClientId && !googleHidden && (
        <div className="animate-fade space-y-3">
          <div className="flex items-center gap-3 text-xs text-muted" role="separator">
            <span className="h-px flex-1 bg-border" />
            or
            <span className="h-px flex-1 bg-border" />
          </div>
          <GoogleButton clientId={googleClientId} onCredential={onGoogleCredential} disabled={busy} />
          <p className="text-center text-xs text-muted">Google sign-in is for staff and parents. Students use their admission number.</p>
        </div>
      )}

      <p className="text-center text-xs text-muted">Forgotten your password? Ask the school office to reset it.</p>

      {showDemo && (
        <section aria-labelledby="demo-heading" className="rounded-card border border-dashed border-border p-4">
          <h2 id="demo-heading" className="mb-1 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-muted">
            <FlaskConical className="size-3.5" aria-hidden />
            Demo accounts
          </h2>
          <p className="mb-3 text-xs text-muted">Development only. Pick one to fill the form.</p>
          <div className="flex flex-wrap gap-2">
            {demoAccounts.map((a) => (
              <button
                key={a.userName}
                type="button"
                onClick={() => fillDemo(a.userName)}
                className="min-h-9 rounded-full border border-border bg-surface px-3 text-xs font-medium text-text transition-colors hover:border-brand hover:text-brand"
                title={a.userName}
              >
                {a.label}
              </button>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
