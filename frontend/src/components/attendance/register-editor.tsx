"use client";

import { useState } from "react";
import { CheckCheck, Info, Lock, Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { AttendanceStatus, ClassRegister } from "@/lib/api/types";
import { saveRegister } from "@/lib/attendance/actions";
import { NOTE_MAX, STATUSES, countMarks, marksDirty, marksFrom, noteErrors, registerPayload, type RegisterMarks } from "@/lib/attendance/rules";
import { runAction } from "@/lib/client/action";
import { useDraft } from "@/lib/drafts/use-draft";
import { Badge, Button, Card, Input, cn } from "@/components/ui/primitives";
import { Alert, EmptyState } from "@/components/ui/states";
import { DraftBanner } from "@/components/drafts/draft-banner";

const timeFormat = new Intl.DateTimeFormat("en-NG", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });

const statusStyles: Record<AttendanceStatus, string> = {
  Present: "peer-checked:bg-success peer-checked:text-white peer-checked:border-success",
  Absent: "peer-checked:bg-danger peer-checked:text-white peer-checked:border-danger",
  Late: "peer-checked:bg-warning peer-checked:text-white peer-checked:border-warning",
  Excused: "peer-checked:bg-brand peer-checked:text-on-brand peer-checked:border-brand",
};

/**
 * One class's register for one day (E9.1), phone-first: a P/A/L/E control per student, a note when
 * not present, counts and Save in a sticky footer. Read-only when the API says so (`canEdit`), with
 * the reason; rows whose term report is published are locked. Unsaved marks survive a reload.
 */
export function RegisterEditor({ initial, readOnlyHint }: { initial: ClassRegister; readOnlyHint: string | null }) {
  const [register, setRegister] = useState(initial);
  const [saved, setSaved] = useState(() => marksFrom(initial));
  const [marks, setMarks] = useState<RegisterMarks>(() => marksFrom(initial));
  const [savedAt, setSavedAt] = useState<Date | null>(null);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);

  const editable = register.canEdit && !register.problem;
  const dirty = marksDirty(marks, saved);
  const errors = noteErrors(marks);
  const counts = countMarks(marks);
  const draft = useDraft({
    scope: ["register", register.classRoomId, register.date],
    value: marks,
    dirty,
    server: saved,
    editable,
    onRestore: (v) => setMarks((m) => Object.fromEntries(Object.keys(m).map((id) => [id, v[id] ?? m[id]]))),
  });

  const set = (id: string, patch: Partial<RegisterMarks[string]>) => setMarks((m) => ({ ...m, [id]: { ...m[id], ...patch } }));

  const save = async (next: RegisterMarks = marks) => {
    setProblem(null);
    if (Object.keys(noteErrors(next)).length > 0) return;
    setBusy(true);
    const result = await runAction(() => saveRegister({ classRoomId: register.classRoomId, date: register.date, entries: registerPayload(register, next) }), {
      success: "Register saved",
      onProblem: setProblem,
    });
    setBusy(false);
    if (result) {
      const m = marksFrom(result);
      setRegister(result);
      setSaved(m);
      setMarks(m);
      setSavedAt(new Date());
      draft.clear();
    }
  };

  const allPresent = () => {
    const next = Object.fromEntries(register.entries.map((e) => [e.studentId, e.locked ? marks[e.studentId] : { status: "Present" as const, note: "" }]));
    setMarks(next);
    void save(next);
  };

  if (register.problem) {
    return (
      <Card>
        <EmptyState icon={Info} title="No register this day">
          {register.problem}
        </EmptyState>
      </Card>
    );
  }
  if (register.entries.length === 0) {
    return (
      <Card>
        <EmptyState title="No students in this class">Admit students into {register.classRoom} first.</EmptyState>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      {editable && <DraftBanner draft={draft} onDiscard={() => { draft.clear(); setMarks(saved); }} />}
      {!editable && readOnlyHint && (
        <Alert tone="info" title="View only">
          {readOnlyHint}
        </Alert>
      )}
      {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
        <Alert tone="danger" title="Couldn't save the register">
          {problemMessage(problem)}
        </Alert>
      )}

      {editable && !register.taken && !dirty && (
        <Button size="lg" className="w-full" onClick={allPresent} loading={busy} loadingText="Saving…">
          <CheckCheck className="size-5" aria-hidden />
          All present
        </Button>
      )}

      <Card className="overflow-hidden">
        <ul className="divide-y divide-border">
          {register.entries.map((e) => {
            const m = marks[e.studentId];
            const disabled = !editable || e.locked;
            return (
              <li key={e.studentId} className="px-4 py-3">
                <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
                  <div className="min-w-0 flex-1 basis-40">
                    <p className="truncate font-medium">{e.name}</p>
                    <p className="flex items-center gap-1.5 text-xs text-muted">
                      {e.admissionNumber}
                      {e.locked && (
                        <span className="inline-flex items-center gap-1">
                          · <Lock className="size-3" aria-hidden />
                          Report published
                        </span>
                      )}
                    </p>
                  </div>
                  <div role="radiogroup" aria-label={`Attendance for ${e.name}`} className="grid shrink-0 grid-cols-4 gap-1.5">
                    {STATUSES.map((s) => (
                      <label key={s.value} title={s.label} className="relative">
                        <input
                          type="radio"
                          name={`att-${e.studentId}`}
                          value={s.value}
                          checked={m.status === s.value}
                          disabled={disabled}
                          onChange={() => set(e.studentId, { status: s.value })}
                          className="peer sr-only"
                          aria-label={s.label}
                        />
                        <span
                          aria-hidden
                          className={cn(
                            "flex size-11 cursor-pointer items-center justify-center rounded-input border border-border bg-surface text-sm font-semibold transition-colors peer-focus-visible:outline-2 peer-focus-visible:outline-brand peer-disabled:cursor-not-allowed peer-disabled:opacity-60",
                            statusStyles[s.value],
                          )}
                        >
                          {s.short}
                        </span>
                      </label>
                    ))}
                  </div>
                </div>
                {m.status !== "Present" && (
                  <div className="mt-2">
                    <Input
                      aria-label={`Note for ${e.name} (optional)`}
                      placeholder="Note (optional), e.g. sick, hospital visit"
                      value={m.note}
                      maxLength={NOTE_MAX + 50}
                      disabled={disabled}
                      onChange={(ev) => set(e.studentId, { note: ev.target.value })}
                      aria-invalid={errors[e.studentId] ? true : undefined}
                    />
                    {errors[e.studentId] && <p className="mt-1 text-sm text-danger">{errors[e.studentId]}</p>}
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      </Card>

      <div className="sticky bottom-0 z-10 -mx-4 border-t border-border bg-surface/95 px-4 py-3 backdrop-blur sm:mx-0 sm:rounded-card sm:border sm:shadow-low">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
          <p className="tabular flex flex-1 flex-wrap gap-1.5 text-sm" aria-live="polite">
            <Badge tone="success">{counts.Present} present</Badge>
            <Badge tone="danger">{counts.Absent} absent</Badge>
            <Badge tone="warning">{counts.Late} late</Badge>
            <Badge>{counts.Excused} excused</Badge>
          </p>
          <span className="text-xs text-muted">
            {dirty ? "Unsaved changes" : savedAt ? `Saved ${timeFormat.format(savedAt)}` : register.taken ? "Register taken" : "Not taken yet"}
          </span>
          {editable && (
            <Button onClick={() => void save()} loading={busy} loadingText="Saving…" disabled={!dirty || Object.keys(errors).length > 0}>
              <Save className="size-4" aria-hidden />
              Save register
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
