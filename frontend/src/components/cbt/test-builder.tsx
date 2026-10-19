"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { ArrowDown, ArrowUp, CheckCircle2, CircleAlert, Plus, Save, Send, Trash2, Undo2, X } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { publishTest, saveTestSections, unpublishTest } from "@/lib/cbt/actions";
import { DIFFICULTIES, KINDS, difficultyTone, firstLine, kindLabel, type Difficulty, type QuestionKind } from "@/lib/cbt/questions";
import {
  MAX_SECTIONS,
  formatMarks,
  newSection,
  scalingLine,
  sectionDrafts,
  sectionErrors,
  toSectionsInput,
  totals,
  type ComponentOption,
  type SectionDraft,
  type TestDetail,
} from "@/lib/cbt/tests";
import { formatLagos } from "@/lib/time/lagos";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Card, Input, Select, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { BankPicker } from "./bank-picker";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

/**
 * E16.2 sections step, totals and publishing. Sections are edited locally and saved together (the API
 * replaces them all); the readiness checklist is the server's view of the saved test.
 */
export function TestBuilder({ initial, components, topics }: { initial: TestDetail; components: ComponentOption[]; topics: string[] }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [test, setTest] = useState(initial);
  const [drafts, setDrafts] = useState(() => sectionDrafts(initial));
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [confirmPublish, setConfirmPublish] = useState(false);
  const [confirmUnpublish, setConfirmUnpublish] = useState(false);
  const editable = test.status === "Draft" && test.canEdit;
  const errors = sectionErrors(drafts);
  const sum = totals(drafts);
  const component = components.find((c) => c.id === test.componentId) ?? (test.scaling ? { id: test.scaling.componentId, name: test.scaling.component, maxScore: test.scaling.componentMax } : null);

  const used = new Map<string, string>();
  for (const s of drafts) for (const q of s.questions) used.set(q.id, s.title || "another section");

  const change = (key: string, patch: Partial<SectionDraft>) => {
    setDrafts((cur) => cur.map((s) => (s.key === key ? { ...s, ...patch, availableInBank: "randomCount" in patch || "topic" in patch || "difficulty" in patch || "kind" in patch ? null : s.availableInBank } : s)));
    setDirty(true);
  };
  const move = <T,>(list: T[], from: number, to: number) => {
    const next = [...list];
    const [item] = next.splice(from, 1);
    next.splice(to, 0, item);
    return next;
  };
  const accept = (r: TestDetail) => {
    setTest(r);
    setDrafts(sectionDrafts(r));
    setDirty(false);
    startTransition(() => router.refresh());
  };

  const save = async () => {
    setProblem(null);
    setSaving(true);
    const r = await runAction(() => saveTestSections(test.id, toSectionsInput(drafts)), { success: "Sections saved", onProblem: setProblem });
    setSaving(false);
    if (r) accept(r);
  };

  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_20rem] lg:items-start">
      <div className="space-y-4">
        {problem && (
          <Alert tone="danger" title="Couldn't save the sections">
            {problem.errors ? Object.values(problem.errors).flat().join(" ") : problemMessage(problem)}
          </Alert>
        )}
        {drafts.length === 0 && (
          <Card className="p-6 text-center text-sm text-muted">{editable ? "No sections yet. Each section holds one kind of question with its own marks." : "This test has no sections."}</Card>
        )}
        {drafts.map((s, i) => (
          <SectionCard
            key={s.key}
            section={s}
            index={i}
            count={drafts.length}
            marks={sum.marks[i]}
            error={errors[s.key]}
            editable={editable}
            topics={topics}
            used={used}
            subjectId={test.subjectId}
            classLevelId={test.classLevelId}
            onChange={(patch) => change(s.key, patch)}
            onMove={(to) => {
              setDrafts((cur) => move(cur, i, to));
              setDirty(true);
            }}
            onRemove={() => {
              setDrafts((cur) => cur.filter((x) => x.key !== s.key));
              setDirty(true);
            }}
            move={move}
          />
        ))}
        {editable && (
          <div className="flex flex-wrap items-center gap-2">
            <Button
              type="button"
              variant="secondary"
              disabled={drafts.length >= MAX_SECTIONS}
              onClick={() => {
                setDrafts((cur) => [...cur, newSection(cur.at(-1)?.kind ?? "Objective", cur.length)]);
                setDirty(true);
              }}
            >
              <Plus className="size-4" aria-hidden />
              Add section
            </Button>
            {dirty && (
              <Button
                type="button"
                variant="ghost"
                onClick={() => {
                  setDrafts(sectionDrafts(test));
                  setDirty(false);
                  setProblem(null);
                }}
              >
                <Undo2 className="size-4" aria-hidden />
                Undo changes
              </Button>
            )}
            <Button type="button" className="ml-auto" loading={saving} loadingText="Saving…" disabled={!dirty || Object.keys(errors).length > 0} onClick={() => void save()}>
              <Save className="size-4" aria-hidden />
              Save sections
            </Button>
          </div>
        )}
      </div>

      <aside className="space-y-4 lg:sticky lg:top-4">
        <Card className="p-5" aria-labelledby="totals-title">
          <h2 id="totals-title" className="font-display text-lg font-semibold">
            Totals
          </h2>
          <ul className="mt-3 space-y-1.5 text-sm">
            {drafts.map((s, i) => (
              <li key={s.key} className="flex justify-between gap-3">
                <span className="min-w-0 truncate">{s.title || `Section ${i + 1}`}</span>
                <span className="tabular shrink-0 text-muted">
                  {s.mode === "Fixed" ? s.questions.length : s.randomCount || 0} × {s.marksPerQuestion || 0} = <span className="font-medium text-text">{formatMarks(sum.marks[i])}</span>
                </span>
              </li>
            ))}
          </ul>
          <p className="mt-3 flex justify-between border-t border-border pt-3 font-medium">
            <span>
              {sum.questionCount} {sum.questionCount === 1 ? "question" : "questions"}
            </span>
            <span className="tabular" data-testid="total-marks">
              {formatMarks(sum.totalMarks)} marks
            </span>
          </p>
          <p className="mt-2 text-sm text-muted" data-testid="scaling-line">
            {scalingLine(sum.totalMarks, component)}
          </p>
        </Card>

        <Card className="p-5" aria-labelledby="ready-title">
          <h2 id="ready-title" className="font-display text-lg font-semibold">
            {test.status === "Published" ? "Published" : "Ready to publish?"}
          </h2>
          {test.status === "Published" ? (
            <>
              <p className="mt-2 text-sm text-muted">
                {test.publishedAt ? `Published ${formatLagos(test.publishedAt)}. ` : ""}Classes see it from {formatLagos(test.opensAt)} to {formatLagos(test.closesAt)}. Unpublish to change it.
              </p>
              {/* canEdit only means "is a draft"; anyone who can open a test can manage it. */}
              <Button type="button" variant="secondary" className="mt-4 w-full" onClick={() => setConfirmUnpublish(true)}>
                <Undo2 className="size-4" aria-hidden />
                Unpublish
              </Button>
            </>
          ) : (
            <>
              {dirty ? (
                <p className="mt-2 text-sm text-muted">Save the sections to update this checklist.</p>
              ) : test.problems.length === 0 ? (
                <p className="mt-2 flex items-center gap-1.5 text-sm text-success">
                  <CheckCircle2 className="size-4" aria-hidden />
                  Everything&apos;s in place.
                </p>
              ) : (
                <ul className="mt-2 space-y-1.5 text-sm" aria-label="Still to do before publishing">
                  {test.problems.map((p) => (
                    <li key={p} className="flex gap-1.5">
                      <CircleAlert className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
                      {p}
                    </li>
                  ))}
                </ul>
              )}
              {editable && (
                <Button type="button" className="mt-4 w-full" disabled={dirty || test.problems.length > 0} onClick={() => setConfirmPublish(true)}>
                  <Send className="size-4" aria-hidden />
                  Publish
                </Button>
              )}
            </>
          )}
        </Card>
      </aside>

      <ConfirmDialog
        open={confirmPublish}
        onOpenChange={setConfirmPublish}
        title={`Publish “${test.title}”?`}
        confirmLabel="Publish"
        pendingLabel="Publishing…"
        onConfirm={async () => {
          const r = await runAction(() => publishTest(test.id), { success: "Test published", onProblem: setProblem });
          if (r) accept(r);
          return r !== null;
        }}
      >
        <p>
          Students in {test.classRooms.length} {test.classRooms.length === 1 ? "class" : "classes"} ({test.classRooms.map((c) => c.name).join(", ")}) will see it from {formatLagos(test.opensAt)}. You can&apos;t change it while
          it&apos;s published.
        </p>
      </ConfirmDialog>
      <ConfirmDialog
        open={confirmUnpublish}
        onOpenChange={setConfirmUnpublish}
        title="Unpublish this test?"
        confirmLabel="Unpublish"
        pendingLabel="Unpublishing…"
        onConfirm={async () => {
          const r = await runAction(() => unpublishTest(test.id), { success: "Test back in draft", onProblem: setProblem });
          if (r) accept(r);
          return r !== null;
        }}
      >
        <p>Students won&apos;t see it until you publish it again. Once students have started, it can&apos;t be unpublished.</p>
      </ConfirmDialog>
    </div>
  );
}

function SectionCard({
  section: s,
  index,
  count,
  marks,
  error,
  editable,
  topics,
  used,
  subjectId,
  classLevelId,
  onChange,
  onMove,
  onRemove,
  move,
}: {
  section: SectionDraft;
  index: number;
  count: number;
  marks: number;
  error?: string;
  editable: boolean;
  topics: string[];
  used: Map<string, string>;
  subjectId: string;
  classLevelId: string;
  onChange: (patch: Partial<SectionDraft>) => void;
  onMove: (to: number) => void;
  onRemove: () => void;
  move: <T>(list: T[], from: number, to: number) => T[];
}) {
  const id = `sec-${s.key}`;
  const label = s.title || `Section ${index + 1}`;
  const disabled = !editable;

  return (
    <Card role="group" className="p-5" aria-labelledby={`${id}-heading`}>
      <div className="flex flex-wrap items-center gap-2">
        <h2 id={`${id}-heading`} className="min-w-0 flex-1 font-display text-lg font-semibold break-words">
          {label}
        </h2>
        <span className="tabular text-sm text-muted">{formatMarks(marks)} marks</span>
        {editable && (
          <span className="flex">
            <Button type="button" size="sm" variant="ghost" aria-label={`Move ${label} up`} disabled={index === 0} onClick={() => onMove(index - 1)}>
              <ArrowUp className="size-4" aria-hidden />
            </Button>
            <Button type="button" size="sm" variant="ghost" aria-label={`Move ${label} down`} disabled={index === count - 1} onClick={() => onMove(index + 1)}>
              <ArrowDown className="size-4" aria-hidden />
            </Button>
            <Button type="button" size="sm" variant="ghost" aria-label={`Remove ${label}`} onClick={onRemove}>
              <Trash2 className="size-4" aria-hidden />
            </Button>
          </span>
        )}
      </div>

      <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="space-y-1.5 sm:col-span-2">
          <label htmlFor={`${id}-title`} className="block text-sm font-medium">
            Section title
          </label>
          <Input id={`${id}-title`} value={s.title} onChange={(e) => onChange({ title: e.target.value })} disabled={disabled} maxLength={100} />
        </div>
        <div className="space-y-1.5">
          <label htmlFor={`${id}-kind`} className="block text-sm font-medium">
            Kind
          </label>
          <Select id={`${id}-kind`} value={s.kind} onChange={(e) => onChange({ kind: e.target.value as QuestionKind })} disabled={disabled || s.questions.length > 0} aria-describedby={s.questions.length > 0 && editable ? `${id}-kind-hint` : undefined}>
            {KINDS.map((k) => (
              <option key={k} value={k}>
                {kindLabel[k]}
              </option>
            ))}
          </Select>
          {s.questions.length > 0 && editable && (
            <p id={`${id}-kind-hint`} className="text-xs text-muted">
              Remove the questions to change it.
            </p>
          )}
        </div>
        <div className="space-y-1.5">
          <label htmlFor={`${id}-marks`} className="block text-sm font-medium">
            Marks per question
          </label>
          <Input id={`${id}-marks`} value={s.marksPerQuestion} onChange={(e) => onChange({ marksPerQuestion: e.target.value })} inputMode="decimal" className="tabular" disabled={disabled} />
        </div>
      </div>

      <fieldset className="mt-4">
        <legend className="mb-1.5 text-sm font-medium">Questions</legend>
        <div className="inline-flex rounded-input bg-surface-2 p-1" role="radiogroup" aria-label={`How ${label} gets its questions`}>
          {(
            [
              ["Fixed", "Chosen from the bank"],
              ["Random", "Drawn at random"],
            ] as const
          ).map(([mode, text]) => (
            <label key={mode} className={cn("inline-flex min-h-9 cursor-pointer items-center rounded-[6px] px-3 text-sm", s.mode === mode ? "bg-surface font-medium shadow-low" : "text-muted", disabled && "cursor-default")}>
              <input type="radio" className="sr-only" name={`${id}-mode`} checked={s.mode === mode} disabled={disabled} onChange={() => onChange({ mode })} />
              {text}
            </label>
          ))}
        </div>
      </fieldset>

      {s.mode === "Fixed" ? (
        <div className="mt-3 space-y-2">
          {s.questions.length === 0 ? (
            <p className="text-sm text-muted">No questions chosen yet.</p>
          ) : (
            <ol className="space-y-1.5" aria-label={`Questions in ${label}`}>
              {s.questions.map((q, qi) => (
                <li key={q.id} className="flex items-start gap-2 rounded-input border border-border px-3 py-2 text-sm">
                  <span className="tabular w-6 shrink-0 pt-0.5 text-muted">{qi + 1}.</span>
                  <span className="min-w-0 flex-1">
                    <span className="block break-words">{firstLine(q.text, 160)}</span>
                    <span className="mt-1 flex flex-wrap gap-1.5">
                      <Badge tone={difficultyTone[q.difficulty]}>{q.difficulty}</Badge>
                      {q.topic && <span className="text-xs text-muted">{q.topic}</span>}
                      {q.isArchived && <Badge tone="danger">Archived: remove it</Badge>}
                    </span>
                  </span>
                  {editable && (
                    <span className="flex shrink-0">
                      <Button type="button" size="sm" variant="ghost" aria-label={`Move question ${qi + 1} up`} disabled={qi === 0} onClick={() => onChange({ questions: move(s.questions, qi, qi - 1) })}>
                        <ArrowUp className="size-4" aria-hidden />
                      </Button>
                      <Button type="button" size="sm" variant="ghost" aria-label={`Move question ${qi + 1} down`} disabled={qi === s.questions.length - 1} onClick={() => onChange({ questions: move(s.questions, qi, qi + 1) })}>
                        <ArrowDown className="size-4" aria-hidden />
                      </Button>
                      <Button type="button" size="sm" variant="ghost" aria-label={`Remove question ${qi + 1}`} onClick={() => onChange({ questions: s.questions.filter((x) => x.id !== q.id) })}>
                        <X className="size-4" aria-hidden />
                      </Button>
                    </span>
                  )}
                </li>
              ))}
            </ol>
          )}
          {editable && <BankPicker subjectId={subjectId} classLevelId={classLevelId} kind={s.kind} topics={topics} used={used} onAdd={(picked) => onChange({ questions: [...s.questions, ...picked] })} />}
        </div>
      ) : (
        <div className="mt-3 grid gap-4 sm:grid-cols-3">
          <div className="space-y-1.5">
            <label htmlFor={`${id}-count`} className="block text-sm font-medium">
              Questions per student
            </label>
            <Input id={`${id}-count`} value={s.randomCount} onChange={(e) => onChange({ randomCount: e.target.value })} inputMode="numeric" className="tabular" disabled={disabled} />
          </div>
          <div className="space-y-1.5">
            <label htmlFor={`${id}-topic`} className="block text-sm font-medium">
              Topic
            </label>
            <Select id={`${id}-topic`} value={s.topic} onChange={(e) => onChange({ topic: e.target.value })} disabled={disabled}>
              <option value="">Any topic</option>
              {[...new Set([...topics, ...(s.topic ? [s.topic] : [])])].map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </Select>
          </div>
          <div className="space-y-1.5">
            <label htmlFor={`${id}-difficulty`} className="block text-sm font-medium">
              Difficulty
            </label>
            <Select id={`${id}-difficulty`} value={s.difficulty} onChange={(e) => onChange({ difficulty: e.target.value as "" | Difficulty })} disabled={disabled}>
              <option value="">Any difficulty</option>
              {DIFFICULTIES.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </Select>
          </div>
          <p className="text-sm text-muted sm:col-span-3" aria-live="polite">
            {s.availableInBank === null
              ? "Save the sections to check how many questions match in the bank."
              : `${s.availableInBank} ${s.availableInBank === 1 ? "question matches" : "questions match"} in the bank. Each student gets a different draw.`}
          </p>
        </div>
      )}

      <details className="mt-4" open={Boolean(s.instructions)}>
        <summary className="cursor-pointer text-sm text-brand">Instructions for this section (optional)</summary>
        <textarea aria-label={`Instructions for ${label}`} value={s.instructions} onChange={(e) => onChange({ instructions: e.target.value })} rows={2} maxLength={1000} disabled={disabled} className={cn(textareaClass, "mt-2")} />
      </details>

      {error && (
        <p className="mt-3 text-sm text-danger" role="alert">
          {error}
        </p>
      )}
    </Card>
  );
}
