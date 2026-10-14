"use client";

import { useFormDraft } from "@/lib/drafts/use-form-draft";
import { DraftBanner } from "@/components/drafts/draft-banner";
import { useState, type ReactNode } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { CalendarClock, Pencil, Plus } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { SessionDto, TermDto, TermNumber } from "@/lib/api/types";
import { addTerm, createSession, makeTermCurrent, updateSession, updateTerm } from "@/lib/academics/actions";
import { runAction } from "@/lib/client/action";
import { formatRange, sessionSchema, termPayload, termSchema, type SessionInput, type TermInput } from "@/lib/validation/academics";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, ConfirmDialog, DialogActions, FormDialog } from "@/components/ui/dialog";

function FormProblem({ problem }: { problem: Problem | null }) {
  if (!problem || problem.status === 0 || problem.status === 429 || problem.status >= 500) return null;
  return (
    <Alert tone="danger" className="animate-rise mb-4" title="Couldn't save">
      <p>{problemMessage(problem)}</p>
    </Alert>
  );
}

/* ----------------------------------------------------------------- sessions */

function SessionForm({ session, suggestedName, close }: { session?: SessionDto; suggestedName?: string; close: () => void }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const form = useForm<SessionInput>({
    resolver: zodResolver(sessionSchema),
    defaultValues: { name: session?.name ?? suggestedName ?? "", startsOn: session?.startsOn ?? "", endsOn: session?.endsOn ?? "" },
  });
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = form;
  const draft = useFormDraft(form, { scope: ["session", session?.id ?? "new"] });

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    const saved = await runAction(() => (session ? updateSession(session.id, values) : createSession(values)), {
      success: (s) => (session ? `Session ${s.name} updated` : `Session ${s.name} created`),
      fields: ["name", "startsOn", "endsOn"] as const,
      setError,
      onProblem: (p) => (p.code === "session.duplicate" ? setError("name", { type: "server", message: problemMessage(p) }) : setProblem(p)),
    });
    if (saved) {
      draft.clear();
      close();
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      <DraftBanner draft={draft} onDiscard={draft.discard} />
      <FormProblem problem={problem} />
      <Field id="session-name" label="Session" error={errors.name?.message} hint="Two consecutive years, e.g. 2027/2028.">
        <Input {...describedBy("session-name", errors.name?.message, true)} {...register("name")} inputMode="numeric" placeholder="2027/2028" autoComplete="off" />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="session-starts" label="Starts on" error={errors.startsOn?.message}>
          <Input {...describedBy("session-starts", errors.startsOn?.message)} {...register("startsOn")} type="date" />
        </Field>
        <Field id="session-ends" label="Ends on" error={errors.endsOn?.message}>
          <Input {...describedBy("session-ends", errors.endsOn?.message)} {...register("endsOn")} type="date" />
        </Field>
      </div>
      <DialogActions>
        <CancelButton />
        <Button type="submit" loading={isSubmitting} loadingText="Saving…">
          {session ? "Save changes" : "Create session"}
        </Button>
      </DialogActions>
    </form>
  );
}

export function NewSessionButton({ suggestedName }: { suggestedName: string }) {
  return (
    <FormDialog
      title="New session"
      description="Add the school year first, then its terms."
      trigger={
        <Button>
          <Plus className="size-4" aria-hidden />
          New session
        </Button>
      }
    >
      {(close) => <SessionForm suggestedName={suggestedName} close={close} />}
    </FormDialog>
  );
}

export function EditSessionButton({ session }: { session: SessionDto }) {
  return (
    <FormDialog
      title={`Edit ${session.name}`}
      description="A session can't be shortened so that its terms fall outside it."
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Edit session ${session.name}`}>
          <Pencil className="size-4" aria-hidden />
          <span className="hidden sm:inline">Edit</span>
        </Button>
      }
    >
      {(close) => <SessionForm session={session} close={close} />}
    </FormDialog>
  );
}

/* -------------------------------------------------------------------- terms */

const termFields = ["number", "startsOn", "endsOn", "midTermStartsOn", "midTermEndsOn"] as const;

function TermForm({ session, term, number, close }: { session: SessionDto; term?: TermDto; number: TermNumber; close: () => void }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const form = useForm<TermInput>({
    resolver: zodResolver(termSchema({ sessionStartsOn: session.startsOn, sessionEndsOn: session.endsOn })),
    defaultValues: {
      number: term?.number ?? number,
      startsOn: term?.startsOn ?? "",
      endsOn: term?.endsOn ?? "",
      midTermStartsOn: term?.midTermStartsOn ?? "",
      midTermEndsOn: term?.midTermEndsOn ?? "",
    },
  });
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = form;
  const draft = useFormDraft(form, { scope: ["term", term?.id ?? `${session.id}:${number}`] });

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    const body = termPayload(values);
    const saved = await runAction(() => (term ? updateTerm(term.id, body) : addTerm(session.id, { number: values.number, ...body })), {
      success: (t) => (term ? `${t.name} updated` : `${t.name} added`),
      fields: termFields,
      setError,
      onProblem: setProblem,
    });
    if (saved) {
      draft.clear();
      close();
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      <DraftBanner draft={draft} onDiscard={draft.discard} />
      <FormProblem problem={problem} />
      <div className="flex items-center justify-between rounded-input bg-surface-2 px-3 py-2 text-sm">
        <span className="font-medium">{term?.number ?? number} Term</span>
        <span className="text-muted">{term ? "The term number can't change" : "Terms are added in order"}</span>
      </div>
      {errors.number?.message && <p className="text-sm text-danger">{errors.number.message}</p>}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="term-starts" label="Starts on" error={errors.startsOn?.message}>
          <Input {...describedBy("term-starts", errors.startsOn?.message)} {...register("startsOn")} type="date" min={session.startsOn} max={session.endsOn} />
        </Field>
        <Field id="term-ends" label="Ends on" error={errors.endsOn?.message}>
          <Input {...describedBy("term-ends", errors.endsOn?.message)} {...register("endsOn")} type="date" min={session.startsOn} max={session.endsOn} />
        </Field>
      </div>
      <fieldset className="rounded-input border border-border p-3">
        <legend className="px-1 text-sm font-medium">
          Mid-term break <span className="font-normal text-muted">(optional)</span>
        </legend>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field id="term-mid-starts" label="From" error={errors.midTermStartsOn?.message}>
            <Input {...describedBy("term-mid-starts", errors.midTermStartsOn?.message)} {...register("midTermStartsOn")} type="date" />
          </Field>
          <Field id="term-mid-ends" label="To" error={errors.midTermEndsOn?.message}>
            <Input {...describedBy("term-mid-ends", errors.midTermEndsOn?.message)} {...register("midTermEndsOn")} type="date" />
          </Field>
        </div>
      </fieldset>
      <DialogActions>
        <CancelButton />
        <Button type="submit" loading={isSubmitting} loadingText="Saving…">
          {term ? "Save changes" : "Add term"}
        </Button>
      </DialogActions>
    </form>
  );
}

export function AddTermButton({ session, number }: { session: SessionDto; number: TermNumber }) {
  return (
    <FormDialog
      title={`Add ${number} Term`}
      description={`Inside ${session.name}: ${formatRange(session.startsOn, session.endsOn)}.`}
      trigger={
        <Button variant="secondary" size="sm">
          <Plus className="size-4" aria-hidden />
          Add {number} Term
        </Button>
      }
    >
      {(close) => <TermForm session={session} number={number} close={close} />}
    </FormDialog>
  );
}

export function EditTermButton({ session, term }: { session: SessionDto; term: TermDto }) {
  return (
    <FormDialog
      title={`Edit ${term.name}`}
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Edit ${term.name}`}>
          <Pencil className="size-4" aria-hidden />
        </Button>
      }
    >
      {(close) => <TermForm session={session} term={term} number={term.number} close={close} />}
    </FormDialog>
  );
}

export function MakeCurrentButton({ term, current }: { term: TermDto; current?: string }): ReactNode {
  return (
    <ConfirmDialog
      title={`Make ${term.name} current?`}
      confirmLabel="Make current"
      pendingLabel="Updating…"
      onConfirm={async () => (await runAction(() => makeTermCurrent(term.id), { success: `${term.name} is now the current term` })) !== null}
      trigger={
        <Button variant="secondary" size="sm">
          <CalendarClock className="size-4" aria-hidden />
          Make current
        </Button>
      }
    >
      <p>This becomes the current term for everyone: staff, parents and students.</p>
      {current && <p className="mt-2">It replaces <span className="font-medium text-text">{current}</span>.</p>}
    </ConfirmDialog>
  );
}
