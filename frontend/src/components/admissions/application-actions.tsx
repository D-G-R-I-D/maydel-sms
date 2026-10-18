"use client";

import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { CreditCard, KeyRound, Lock, Plus } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { AdmissionsInfo, Application } from "@/lib/api/types";
import { applyForChild, payForm, redeemFormCode } from "@/lib/admissions/actions";
import { childSchema, formCodeError, normalizeFormCode, type ChildFormValues, type ChildPayload } from "@/lib/admissions/rules";
import { formatNaira } from "@/lib/finance/rules";
import { runAction } from "@/lib/client/action";
import { unreachableProblem } from "@/lib/client/unreachable";
import { Button, Field, Input, Select, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, DialogActions, FormDialog } from "@/components/ui/dialog";

/** Pay the form fee online (Paystack). Without keys (422 payment.online_unavailable) → pay at the school and use a code. */
export function PayFormButton({ application }: { application: Pick<Application, "id" | "formFee"> }) {
  const [busy, setBusy] = useState(false);
  const [unavailable, setUnavailable] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);

  if (unavailable) {
    return (
      <p className="flex items-start gap-1.5 text-sm text-muted">
        <Lock className="mt-0.5 size-4 shrink-0" aria-hidden />
        Online payment isn&apos;t available yet. Pay at the school and enter your form code.
      </p>
    );
  }
  return (
    <div className="space-y-2">
      <Button
        loading={busy}
        loadingText="Opening Paystack…"
        onClick={async () => {
          setProblem(null);
          setBusy(true);
          let r: Awaited<ReturnType<typeof payForm>>;
          try {
            r = await payForm(application.id);
          } catch {
            setBusy(false);
            setProblem(unreachableProblem());
            return;
          }
          if (r.ok) {
            window.location.assign(r.data.authorizationUrl);
            return;
          }
          setBusy(false);
          if (r.problem.code === "payment.online_unavailable") setUnavailable(true);
          else setProblem(r.problem);
        }}
      >
        <CreditCard className="size-4" aria-hidden />
        Pay {formatNaira(application.formFee)} online
      </Button>
      {problem && <p className="text-sm text-danger">{problemMessage(problem)}</p>}
    </div>
  );
}

/** "I have a form code": the code from the bursar's receipt after paying at the school. */
export function RedeemCodeForm({ applicationId }: { applicationId: string }) {
  const [code, setCode] = useState("");
  const [touched, setTouched] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const error = touched ? formCodeError(code) : null;

  return (
    <form
      className="space-y-2"
      noValidate
      onSubmit={async (e) => {
        e.preventDefault();
        setTouched(true);
        setProblem(null);
        if (formCodeError(code)) return;
        setBusy(true);
        await runAction(() => redeemFormCode(applicationId, normalizeFormCode(code)), {
          success: "Form code accepted: your form is paid",
          onProblem: setProblem,
        });
        setBusy(false);
      }}
    >
      <label htmlFor={`code-${applicationId}`} className="block text-sm font-medium">
        I have a form code
      </label>
      <div className="flex flex-wrap gap-2">
        <Input
          id={`code-${applicationId}`}
          value={code}
          onChange={(e) => setCode(e.target.value)}
          onBlur={() => code && setCode(normalizeFormCode(code))}
          placeholder="ABCD-EFGH"
          autoComplete="off"
          spellCheck={false}
          className="w-44 font-mono tracking-widest uppercase"
          aria-invalid={error ? true : undefined}
        />
        <Button type="submit" variant="secondary" loading={busy} loadingText="Checking…">
          <KeyRound className="size-4" aria-hidden />
          Use code
        </Button>
      </div>
      {(error || (problem && problem.status > 0 && problem.status !== 429)) && (
        <p className="text-sm text-danger" role="alert">
          {error ?? (problem!.errors?.code?.join(" ") || problemMessage(problem!))}
        </p>
      )}
    </form>
  );
}

/** Apply for another child from the same family account. */
export function ApplyAnotherChild({ sessions }: { sessions: AdmissionsInfo[] }) {
  return (
    <FormDialog
      title="Apply for another child"
      description="You'll pay a separate form fee for each child."
      trigger={
        <Button>
          <Plus className="size-4" aria-hidden />
          Apply for another child
        </Button>
      }
    >
      {(close) => <ChildForm sessions={sessions} close={close} />}
    </FormDialog>
  );
}

function ChildForm({ sessions, close }: { sessions: AdmissionsInfo[]; close: () => void }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = useForm<ChildFormValues, unknown, ChildPayload>({
    resolver: zodResolver(childSchema),
    defaultValues: { childFirstName: "", childLastName: "", childDateOfBirth: "", classLevelId: "", sessionId: sessions[0]?.sessionId ?? "" },
  });
  const sessionId = useWatch({ control, name: "sessionId" });
  const session = sessions.find((s) => s.sessionId === sessionId) ?? sessions[0];

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    const r = await runAction(() => applyForChild(values), {
      success: (a) => `Application ${a.referenceNumber} started`,
      fields: ["childFirstName", "childLastName", "childDateOfBirth", "classLevelId", "sessionId"] as const,
      setError,
      onProblem: setProblem,
    });
    if (r) close();
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
        <Alert tone="danger" title={problem.code === "admission.closed" ? "Admissions are closed" : "Couldn't start the application"}>
          {problemMessage(problem)}
        </Alert>
      )}
      {sessions.length > 1 && (
        <Field id="ac-session" label="Session" error={errors.sessionId?.message}>
          <Select {...describedBy("ac-session", errors.sessionId?.message)} {...register("sessionId")}>
            {sessions.map((s) => (
              <option key={s.sessionId} value={s.sessionId}>
                {s.session}
              </option>
            ))}
          </Select>
        </Field>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="ac-first" label="First name" error={errors.childFirstName?.message}>
          <Input {...describedBy("ac-first", errors.childFirstName?.message)} {...register("childFirstName")} autoComplete="off" />
        </Field>
        <Field id="ac-last" label="Surname" error={errors.childLastName?.message}>
          <Input {...describedBy("ac-last", errors.childLastName?.message)} {...register("childLastName")} autoComplete="off" />
        </Field>
        <Field id="ac-dob" label="Date of birth (optional)" error={errors.childDateOfBirth?.message}>
          <Input {...describedBy("ac-dob", errors.childDateOfBirth?.message)} {...register("childDateOfBirth")} type="date" />
        </Field>
        <Field id="ac-class" label="Class applying for" error={errors.classLevelId?.message}>
          <Select {...describedBy("ac-class", errors.classLevelId?.message)} {...register("classLevelId")}>
            <option value="">Choose a class</option>
            {(session?.classLevels ?? []).map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <DialogActions>
        <CancelButton />
        <Button type="submit" loading={isSubmitting} loadingText="Starting…">
          Start application
        </Button>
      </DialogActions>
    </form>
  );
}
