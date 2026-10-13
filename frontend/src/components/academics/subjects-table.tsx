"use client";

import { useOptimistic, useState, useTransition } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { BookOpen, Pencil, Plus, Search } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { SubjectDto } from "@/lib/api/types";
import { createSubject, updateSubject } from "@/lib/academics/actions";
import { runAction } from "@/lib/client/action";
import { subjectSchema, type SubjectInput } from "@/lib/validation/academics";
import { Badge, Button, Card, Field, Input, cn, describedBy } from "@/components/ui/primitives";
import { Alert, EmptyState } from "@/components/ui/states";
import { CancelButton, DialogActions, FormDialog } from "@/components/ui/dialog";
import { Switch } from "@/components/ui/switch";
import { toast } from "@/components/ui/toast";

const fields = ["name", "code"] as const;

function SubjectForm({ subject, close }: { subject?: SubjectDto; close: () => void }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = useForm<SubjectInput, unknown, { name: string; code: string; isActive: boolean }>({
    resolver: zodResolver(subjectSchema),
    defaultValues: { name: subject?.name ?? "", code: subject?.code ?? "", isActive: subject?.isActive ?? true },
  });

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    const saved = await runAction(() => (subject ? updateSubject(subject.id, values) : createSubject(values)), {
      success: (s) => (subject ? `${s.name} updated` : `${s.name} added`),
      fields,
      setError,
      onProblem: (p) => (p.code === "subject.duplicate" ? setProblem({ ...p, title: "That subject already exists" }) : setProblem(p)),
    });
    if (saved) close();
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {problem && problem.status > 0 && problem.status !== 429 && problem.status < 500 && (
        <Alert tone="danger" className="animate-rise" title="Couldn't save">
          <p>{problemMessage(problem)}</p>
        </Alert>
      )}
      <Field id="subject-name" label="Name" error={errors.name?.message}>
        <Input {...describedBy("subject-name", errors.name?.message)} {...register("name")} maxLength={100} autoComplete="off" placeholder="Mathematics" />
      </Field>
      <Field id="subject-code" label="Code" error={errors.code?.message} hint="Letters and digits, up to 10. Saved in capitals.">
        <Input
          {...describedBy("subject-code", errors.code?.message, true)}
          {...register("code")}
          maxLength={10}
          autoComplete="off"
          autoCapitalize="characters"
          spellCheck={false}
          placeholder="MTH"
          className="uppercase"
        />
      </Field>
      <Controller
        control={control}
        name="isActive"
        render={({ field }) => (
          <div className="flex items-center justify-between gap-4 rounded-input border border-border p-3">
            <div>
              <p id="subject-active-label" className="text-sm font-medium">
                Active
              </p>
              <p className="text-xs text-muted">Inactive subjects stay on classes that offer them but can&apos;t be newly added.</p>
            </div>
            <Switch checked={field.value} onCheckedChange={field.onChange} label="Active" />
          </div>
        )}
      />
      <DialogActions>
        <CancelButton />
        <Button type="submit" loading={isSubmitting}>
          {subject ? "Save changes" : "Add subject"}
        </Button>
      </DialogActions>
    </form>
  );
}

export function NewSubjectButton() {
  return (
    <FormDialog
      title="New subject"
      trigger={
        <Button>
          <Plus className="size-4" aria-hidden />
          New subject
        </Button>
      }
    >
      {(close) => <SubjectForm close={close} />}
    </FormDialog>
  );
}

function EditSubjectButton({ subject }: { subject: SubjectDto }) {
  return (
    <FormDialog
      title={`Edit ${subject.name}`}
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Edit ${subject.name}`}>
          <Pencil className="size-4" aria-hidden />
        </Button>
      }
    >
      {(close) => <SubjectForm subject={subject} close={close} />}
    </FormDialog>
  );
}

type Filter = "all" | "active" | "inactive";

/** Searchable catalogue with an instant (optimistic) active toggle for admins. */
export function SubjectsTable({ subjects, canEdit }: { subjects: SubjectDto[]; canEdit: boolean }) {
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState<Filter>("all");
  const [, startTransition] = useTransition();
  const [optimistic, setOptimistic] = useOptimistic(subjects, (state, changed: SubjectDto) => state.map((s) => (s.id === changed.id ? changed : s)));

  const toggleActive = (subject: SubjectDto, isActive: boolean) =>
    startTransition(async () => {
      setOptimistic({ ...subject, isActive });
      await runAction(() => updateSubject(subject.id, { name: subject.name, code: subject.code, isActive }), {
        success: isActive ? `${subject.name} is active` : `${subject.name} is now inactive`,
        onProblem: (p) => {
          if (p.status > 0 && p.status < 500 && p.status !== 429) toast.error("Couldn't update the subject", { description: problemMessage(p) });
        },
      });
    });

  const q = query.trim().toLowerCase();
  const rows = optimistic.filter(
    (s) => (filter === "all" || (filter === "active") === s.isActive) && (!q || s.name.toLowerCase().includes(q) || s.code.toLowerCase().includes(q)),
  );
  const counts = { all: optimistic.length, active: optimistic.filter((s) => s.isActive).length, inactive: optimistic.filter((s) => !s.isActive).length };

  if (subjects.length === 0) {
    return (
      <Card>
        <EmptyState icon={BookOpen} title="No subjects yet" action={canEdit ? <NewSubjectButton /> : undefined}>
          {canEdit ? "Add the subjects the school teaches, then choose which classes offer them." : "An administrator hasn't added subjects yet."}
        </EmptyState>
      </Card>
    );
  }

  return (
    <Card className="overflow-hidden">
      <div className="flex flex-col gap-3 border-b border-border p-3 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
          <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search by name or code" aria-label="Search subjects" className="pl-9" />
        </div>
        <div role="radiogroup" aria-label="Show" className="inline-flex rounded-input bg-surface-2 p-1">
          {(["all", "active", "inactive"] as const).map((f) => (
            <button
              key={f}
              type="button"
              role="radio"
              aria-checked={filter === f}
              onClick={() => setFilter(f)}
              className={cn(
                "min-h-9 rounded-[6px] px-3 text-sm capitalize transition-colors",
                filter === f ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text",
              )}
            >
              {f} <span className="tabular text-xs text-muted">{counts[f]}</span>
            </button>
          ))}
        </div>
      </div>

      <ul className="divide-y divide-border">
        {rows.map((s) => (
          <li key={s.id} className={cn("flex items-center gap-3 px-4 py-3 transition-opacity", !s.isActive && "opacity-70")}>
            <Badge className="w-16 justify-center font-mono">{s.code}</Badge>
            <span className="min-w-0 flex-1 truncate font-medium">{s.name}</span>
            {canEdit ? (
              <>
                <span className="hidden text-xs text-muted sm:inline">{s.isActive ? "Active" : "Inactive"}</span>
                <Switch checked={s.isActive} onCheckedChange={(v) => toggleActive(s, v)} label={`${s.name} active`} />
                <EditSubjectButton subject={s} />
              </>
            ) : (
              <Badge tone={s.isActive ? "success" : "neutral"}>{s.isActive ? "Active" : "Inactive"}</Badge>
            )}
          </li>
        ))}
        {rows.length === 0 && <li className="px-4 py-8 text-center text-sm text-muted">No subjects match.</li>}
      </ul>
    </Card>
  );
}

