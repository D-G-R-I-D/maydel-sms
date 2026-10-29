"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { ArrowRight, GraduationCap, RotateCcw, TriangleAlert } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { applyPromotions, undoPromotion } from "@/lib/promotion/actions";
import { choiceError, decisionsFor, doneLabel, initialChoices, outcomes, resultText, summaryText, type Choice, type PromotionClass, type PromotionOutcome, type PromotionPlan } from "@/lib/promotion/promotion";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Badge, Button, Card, Select, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";

export type ClassOption = { id: string; name: string; levelOrder: number; capacity: number; studentCount: number };

const fmtAvg = (n: number | null) => (n == null ? "—" : n.toFixed(1));

function ClassSection({
  cls,
  plan,
  classOptions,
  choices,
  setChoice,
  flaggedOnly,
  onApply,
}: {
  cls: PromotionClass;
  plan: PromotionPlan;
  classOptions: ClassOption[];
  choices: Record<string, Choice>;
  setChoice: (studentId: string, c: Choice) => void;
  flaggedOnly: boolean;
  onApply: (classes: PromotionClass[]) => Promise<boolean>;
}) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const pending = cls.students.filter((s) => !s.done);
  const doneCount = cls.students.length - pending.length;
  const decisions = decisionsFor([cls], choices);
  const errors = pending.filter((s) => choices[s.studentId] && choiceError(choices[s.studentId], cls.classRoomId)).length;
  const targets = classOptions.filter((o) => o.id !== cls.classRoomId);
  // Upper levels first, then the rest, so "the next class" is near the top.
  const sortedTargets = [...targets.filter((o) => o.levelOrder > cls.levelOrder), ...targets.filter((o) => o.levelOrder <= cls.levelOrder)];
  const promoteTargets = new Set(pending.filter((s) => choices[s.studentId]?.outcome === "Promote").map((s) => choices[s.studentId].toClassRoomId ?? ""));
  const classTarget = promoteTargets.size === 1 ? [...promoteTargets][0] : "";
  const rows = flaggedOnly ? cls.students.filter((s) => s.needsReview) : cls.students;
  if (flaggedOnly && rows.length === 0) return null;
  const headId = `pc-${cls.classRoomId}`;

  return (
    <section aria-labelledby={headId} className="overflow-hidden rounded-card border border-border bg-surface shadow-low">
      <div className="flex flex-wrap items-center gap-3 border-b border-border p-4">
        <h2 id={headId} className="flex flex-wrap items-center gap-2 font-display text-lg font-semibold">
          {cls.name}
          <ArrowRight className="size-4 text-muted" aria-hidden />
          {cls.isFinalLevel ? (
            <span className="inline-flex items-center gap-1 text-brand">
              <GraduationCap className="size-4" aria-hidden />
              Graduate
            </span>
          ) : (
            <span className={cls.nextClassName ? "" : "text-warning"}>{cls.nextClassName ?? "Choose a class"}</span>
          )}
        </h2>
        <span className="text-sm text-muted">
          {cls.students.length} students{doneCount > 0 ? ` · ${doneCount} moved on` : ""}
        </span>
        <div className="ml-auto flex flex-wrap items-center gap-2">
          {!cls.isFinalLevel && pending.length > 0 && (
            <>
              <label htmlFor={`${headId}-t`} className="text-sm text-muted">
                Move class to
              </label>
              <Select
                id={`${headId}-t`}
                value={classTarget}
                onChange={(e) => {
                  for (const s of pending) if (choices[s.studentId]?.outcome === "Promote") setChoice(s.studentId, { outcome: "Promote", toClassRoomId: e.target.value || null });
                }}
              >
                <option value="">{promoteTargets.size > 1 ? "Mixed" : "Choose…"}</option>
                {sortedTargets.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.name} ({o.studentCount}/{o.capacity})
                  </option>
                ))}
              </Select>
            </>
          )}
          {doneCount > 0 && (
            <ConfirmDialog
              title={`Undo ${cls.name}?`}
              confirmLabel="Undo"
              pendingLabel="Undoing…"
              tone="destructive"
              onConfirm={async () => {
                const r = await runAction(() => undoPromotion(plan.sessionId, cls.classRoomId), {
                  success: (u) => `${u.undone} put back in ${cls.name}${u.skipped.length ? `. Left alone (changed since): ${u.skipped.join(", ")}` : ""}`,
                });
                if (r) startTransition(() => router.refresh());
                return Boolean(r);
              }}
              trigger={
                <Button variant="secondary" size="sm">
                  <RotateCcw className="size-4" aria-hidden />
                  Undo
                </Button>
              }
            >
              The {doneCount} students moved on from {cls.name} go back to it with their old status. Anyone changed by hand since is left alone.
            </ConfirmDialog>
          )}
          {pending.length > 0 && (
            <ConfirmDialog
              title={`Apply ${cls.name}?`}
              confirmLabel="Apply"
              pendingLabel="Applying…"
              onConfirm={() => onApply([cls])}
              trigger={
                <Button size="sm" disabled={!plan.canPromote || errors > 0 || decisions.length === 0} title={errors > 0 ? "Fix the highlighted rows first" : undefined}>
                  Apply to this class
                </Button>
              }
            >
              {summaryText(decisions)}.
            </ConfirmDialog>
          )}
        </div>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[40rem] text-sm">
          <thead className="bg-surface-2 text-left text-xs text-muted">
            <tr>
              <th scope="col" className="px-4 py-2 font-medium">
                Student
              </th>
              <th scope="col" className="px-4 py-2 text-right font-medium">
                Session avg.
              </th>
              <th scope="col" className="px-4 py-2 font-medium">
                Outcome
              </th>
              <th scope="col" className="px-4 py-2 font-medium">
                Class
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {rows.map((s) => {
              const ch = choices[s.studentId];
              const err = ch ? choiceError(ch, cls.classRoomId) : null;
              const rid = `pr-${s.studentId}`;
              return (
                <tr key={s.studentId} className={cn(s.done && "bg-surface-2/50 text-muted")}>
                  <td className="px-4 py-2">
                    <p className="font-medium text-text">{s.fullName}</p>
                    <p className="text-xs text-muted">
                      {s.admissionNumber}
                      {s.status !== "Active" ? ` · ${s.status}` : ""}
                    </p>
                  </td>
                  <td className="px-4 py-2 text-right">
                    <span className="tabular">{fmtAvg(s.sessionAverage)}</span>
                    {s.needsReview && (
                      <Badge tone="warning" className="ml-2">
                        Review
                      </Badge>
                    )}
                  </td>
                  {s.done ? (
                    <td className="px-4 py-2" colSpan={2}>
                      <Badge tone="success">{doneLabel[s.done.outcome]}</Badge>
                      {s.done.outcome === "Promote" && <span className="ml-2">to {s.done.toClassName}</span>}
                    </td>
                  ) : (
                    <>
                      <td className="px-4 py-2">
                        <label htmlFor={`${rid}-o`} className="sr-only">
                          Outcome for {s.fullName}
                        </label>
                        <Select
                          id={`${rid}-o`}
                          value={ch?.outcome ?? s.suggestedOutcome}
                          onChange={(e) => {
                            const outcome = e.target.value as PromotionOutcome;
                            setChoice(s.studentId, { outcome, toClassRoomId: outcome === "Promote" ? (ch?.toClassRoomId ?? s.suggestedClassRoomId ?? cls.nextClassRoomId) : null });
                          }}
                          className="min-w-32"
                        >
                          {outcomes.map((o) => (
                            <option key={o.value} value={o.value}>
                              {o.label}
                            </option>
                          ))}
                        </Select>
                      </td>
                      <td className="px-4 py-2">
                        {ch?.outcome === "Promote" ? (
                          <>
                            <label htmlFor={`${rid}-c`} className="sr-only">
                              New class for {s.fullName}
                            </label>
                            <Select
                              id={`${rid}-c`}
                              value={ch.toClassRoomId ?? ""}
                              onChange={(e) => setChoice(s.studentId, { outcome: "Promote", toClassRoomId: e.target.value || null })}
                              aria-invalid={err ? true : undefined}
                              aria-describedby={err ? `${rid}-e` : undefined}
                              className="min-w-36"
                            >
                              <option value="">Choose…</option>
                              {sortedTargets.map((o) => (
                                <option key={o.id} value={o.id}>
                                  {o.name}
                                </option>
                              ))}
                            </Select>
                            {err && (
                              <p id={`${rid}-e`} className="mt-1 text-xs text-danger">
                                {err}
                              </p>
                            )}
                          </>
                        ) : (
                          <span className="text-muted">{ch?.outcome === "Repeat" ? `Stays in ${cls.name}` : "Leaves the school"}</span>
                        )}
                      </td>
                    </>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

/** E3.6 admin: every class's students pre-filled with the suggested outcome; change the exceptions, then apply. */
export function PromotionBoard({ plan, classOptions }: { plan: PromotionPlan; classOptions: ClassOption[] }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [choices, setChoices] = useState(() => initialChoices(plan));
  const [flaggedOnly, setFlaggedOnly] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const setChoice = (id: string, c: Choice) => setChoices((m) => ({ ...m, [id]: c }));

  const all = decisionsFor(plan.classes, choices);
  const errors = plan.classes.reduce((t, c) => t + c.students.filter((s) => !s.done && choices[s.studentId] && choiceError(choices[s.studentId], c.classRoomId)).length, 0);
  const flagged = plan.classes.reduce((t, c) => t + c.students.filter((s) => s.needsReview && !s.done).length, 0);

  const apply = async (classes: PromotionClass[]) => {
    setProblem(null);
    const r = await runAction(() => applyPromotions(plan.sessionId, decisionsFor(classes, choices)), { success: resultText, onProblem: setProblem });
    if (r) startTransition(() => router.refresh());
    return Boolean(r);
  };

  return (
    <div className="space-y-5">
      {problem && (
        <Alert tone="danger" title="Nothing was changed">
          {problemMessage(problem)}
        </Alert>
      )}
      <Card className="flex flex-wrap items-center gap-4 p-4">
        <div className="flex items-center gap-3 text-sm">
          <Switch id="pr-flagged" label="Show only flagged" checked={flaggedOnly} onCheckedChange={setFlaggedOnly} />
          <span>
            Show only flagged <span className="text-muted">({flagged} with a session average under 40)</span>
          </span>
        </div>
        <div className="ml-auto flex flex-wrap items-center gap-3">
          {errors > 0 && (
            <span className="inline-flex items-center gap-1 text-sm text-danger">
              <TriangleAlert className="size-4" aria-hidden />
              {errors} need a class
            </span>
          )}
          <ConfirmDialog
            title="Apply to all classes?"
            confirmLabel="Apply to all"
            pendingLabel="Applying…"
            onConfirm={() => apply(plan.classes)}
            trigger={
              <Button disabled={!plan.canPromote || errors > 0 || all.length === 0}>Apply to all classes</Button>
            }
          >
            <span className="block font-medium text-text">{summaryText(all)}.</span>
            <span className="mt-2 block">Everything is applied together, so each class moves up into the room the class above has left. If anything fails, nothing changes.</span>
          </ConfirmDialog>
        </div>
      </Card>

      {plan.classes.map((c) => (
        <ClassSection key={c.classRoomId} cls={c} plan={plan} classOptions={classOptions} choices={choices} setChoice={setChoice} flaggedOnly={flaggedOnly} onApply={apply} />
      ))}
    </div>
  );
}
