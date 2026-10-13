"use client";

import { useEffect, useMemo, useState } from "react";
import { Check, CircleSlash, Search, UserRound } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { ClassSubjectDto, StaffLookupItem, SubjectDto } from "@/lib/api/types";
import { saveClassSubjects } from "@/lib/academics/actions";
import { runAction } from "@/lib/client/action";
import { changedTeachers, diffSelection, editableSubjects } from "@/lib/academics/class-subjects";
import { teacherOptions } from "@/lib/validation/staff";
import { Badge, Button, Card, Input, cn } from "@/components/ui/primitives";
import { Alert, EmptyState } from "@/components/ui/states";

/**
 * Checklist of subjects for one class. Save sends the whole list (the API replaces it), keeping
 * each subject's teacher. Teachers come from the active-staff lookup (part 2).
 */
export function ClassSubjectsEditor({
  classroomId,
  className,
  assigned,
  subjects,
  teachers,
  canEdit,
}: {
  classroomId: string;
  className: string;
  assigned: ClassSubjectDto[];
  subjects: SubjectDto[];
  teachers: StaffLookupItem[];
  canEdit: boolean;
}) {
  const [saved, setSaved] = useState(assigned);
  const [selected, setSelected] = useState<ReadonlySet<string>>(() => new Set(assigned.map((a) => a.subjectId)));
  const [teacherFor, setTeacherFor] = useState<Record<string, string | null>>(() => Object.fromEntries(assigned.map((a) => [a.subjectId, a.teacherId])));
  const [query, setQuery] = useState("");
  const [saving, setSaving] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);

  const rows = useMemo(() => editableSubjects(subjects, saved), [subjects, saved]);
  const visible = rows.filter((r) => `${r.name} ${r.code}`.toLowerCase().includes(query.trim().toLowerCase()));
  const { added, removed, dirty: membershipDirty } = diffSelection(saved.map((a) => a.subjectId), selected);
  const reassigned = changedTeachers(saved, teacherFor, selected);
  const dirty = membershipDirty || reassigned.size > 0;
  const teacherOf = new Map(saved.map((a) => [a.subjectId, a]));
  const hiddenInactive = subjects.filter((s) => !s.isActive && !selected.has(s.id) && !teacherOf.has(s.id)).length;

  // Warn before leaving with unsaved changes.
  useEffect(() => {
    if (!dirty) return;
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);

  const toggle = (id: string) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const selectAllActive = () => setSelected(new Set([...selected, ...rows.filter((r) => r.isActive).map((r) => r.id)]));
  const clearAll = () => setSelected(new Set());
  const discard = () => {
    setSelected(new Set(saved.map((a) => a.subjectId)));
    setTeacherFor(Object.fromEntries(saved.map((a) => [a.subjectId, a.teacherId])));
    setProblem(null);
  };

  const save = async () => {
    setSaving(true);
    setProblem(null);
    const list = rows.filter((r) => selected.has(r.id)).map((r) => ({ subjectId: r.id, teacherId: teacherFor[r.id] ?? null }));
    const result = await runAction(() => saveClassSubjects(classroomId, list), {
      success: `Subjects for ${className} saved`,
      fields: ["subjects"] as const,
      setError: (_f, e) => setProblem({ status: 400, title: e.message }),
      onProblem: setProblem,
    }).finally(() => setSaving(false));
    if (result) {
      setSaved(result);
      setSelected(new Set(result.map((a) => a.subjectId)));
      setTeacherFor(Object.fromEntries(result.map((a) => [a.subjectId, a.teacherId])));
    }
  };

  if (rows.length === 0) {
    return (
      <Card>
        <EmptyState title="No subjects in the catalogue">Add subjects under Academics → Subjects first.</EmptyState>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      {problem && problem.status > 0 && problem.status !== 429 && problem.status < 500 && (
        <Alert tone="danger" className="animate-rise" title={problem.code === "class_subjects.has_scores" ? "Some subjects can't be removed" : "Couldn't save"}>
          <p>{problemMessage(problem)}</p>
        </Alert>
      )}

      <Card className="overflow-hidden">
        <div className="flex flex-col gap-3 border-b border-border p-3 sm:flex-row sm:items-center">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
            <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Filter subjects" aria-label="Filter subjects" className="pl-9" />
          </div>
          <p className="tabular text-sm text-muted sm:px-2" aria-live="polite">
            {selected.size} of {rows.length} offered
          </p>
          {canEdit && (
            <div className="flex gap-1">
              <Button variant="ghost" size="sm" onClick={selectAllActive}>
                Select all
              </Button>
              <Button variant="ghost" size="sm" onClick={clearAll}>
                Clear
              </Button>
            </div>
          )}
        </div>

        <ul className="divide-y divide-border" aria-label={`Subjects offered by ${className}`}>
          {visible.map((s) => {
            const on = selected.has(s.id);
            const current = teacherOf.get(s.id);
            // An inactive subject can be un-ticked and re-ticked until saved; it just can't be newly added.
            const locked = !canEdit || (!s.isActive && !on && !current);
            return (
              <li key={s.id} className={cn("flex flex-wrap items-center gap-3 px-4 py-3 transition-colors", on && "bg-brand/5")}>
                <label className={cn("flex min-h-11 min-w-0 flex-1 items-center gap-3", locked ? "cursor-default" : "cursor-pointer")}>
                  <input
                    type="checkbox"
                    checked={on}
                    disabled={locked}
                    onChange={() => toggle(s.id)}
                    className="size-5 shrink-0 cursor-pointer accent-[var(--brand)] disabled:cursor-not-allowed"
                  />
                  <span className="min-w-0">
                    <span className="flex flex-wrap items-center gap-2 font-medium">
                      {s.name}
                      <Badge>{s.code}</Badge>
                      {!s.isActive && (
                        <Badge tone="warning">
                          <CircleSlash className="size-3" aria-hidden />
                          Inactive
                        </Badge>
                      )}
                      {added.has(s.id) && <Badge tone="success">Adding</Badge>}
                      {removed.has(s.id) && <Badge tone="danger">Removing</Badge>}
                    </span>
                    {!s.isActive && <span className="block text-xs text-muted">Stays while selected, but can&apos;t be added back once removed.</span>}
                  </span>
                </label>
                <div className="flex w-full items-center gap-2 pl-8 text-sm text-muted sm:w-64 sm:pl-0">
                  <UserRound className="size-4 shrink-0" aria-hidden />
                  {canEdit ? (
                    <select
                      value={on ? teacherFor[s.id] ?? "" : ""}
                      disabled={!on}
                      onChange={(e) => setTeacherFor((prev) => ({ ...prev, [s.id]: e.target.value || null }))}
                      aria-label={`Teacher for ${s.name}`}
                      className={cn(
                        "min-h-9 w-full truncate rounded-input border bg-surface px-2 text-sm text-text disabled:cursor-not-allowed disabled:bg-surface-2 disabled:text-muted",
                        reassigned.has(s.id) ? "border-brand" : "border-border",
                      )}
                    >
                      <option value="">{on ? "No teacher yet" : "—"}</option>
                      {teacherOptions(teachers, { id: current?.teacherId ?? null, name: current?.teacher ?? null }).map((o) => (
                        <option key={o.id} value={o.id}>
                          {o.label}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <span className="truncate">{on ? current?.teacher ?? "No teacher yet" : "—"}</span>
                  )}
                </div>
              </li>
            );
          })}
          {visible.length === 0 && <li className="px-4 py-8 text-center text-sm text-muted">No subjects match &ldquo;{query}&rdquo;.</li>}
        </ul>
        {hiddenInactive > 0 && (
          <p className="border-t border-border px-4 py-2.5 text-xs text-muted">
            {hiddenInactive} inactive {hiddenInactive === 1 ? "subject is" : "subjects are"} hidden: inactive subjects can&apos;t be newly added.
          </p>
        )}
      </Card>

      {canEdit && dirty && (
        <div className="animate-rise sticky bottom-4 z-10">
          <Card className="flex flex-col gap-3 border-brand/40 p-3 shadow-lg sm:flex-row sm:items-center">
            <p className="flex-1 text-sm" aria-live="polite">
              <span className="font-medium">Unsaved changes:</span>{" "}
              {[added.size > 0 && `${added.size} to add`, removed.size > 0 && `${removed.size} to remove`, reassigned.size > 0 && `${reassigned.size} teacher ${reassigned.size === 1 ? "change" : "changes"}`].filter(Boolean).join(", ")}
            </p>
            <div className="flex gap-2">
              <Button variant="secondary" onClick={discard} disabled={saving}>
                Discard
              </Button>
              <Button onClick={save} loading={saving} className="flex-1 sm:flex-none">
                <Check className="size-4" aria-hidden />
                Save subjects
              </Button>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
