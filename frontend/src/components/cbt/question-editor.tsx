"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition } from "react";
import { useFieldArray, useForm, useWatch, type FieldPath } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Archive, ArrowDown, ArrowUp, Eye, Plus, Save, Trash2, X } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import {
  DIFFICULTIES,
  KINDS,
  MAX_OPTIONS,
  MAX_TOPIC,
  addAnswer,
  bankHref,
  blankOption,
  emptyValues,
  gapCount,
  hasAnswers,
  hasGuide,
  hasOptions,
  kindHint,
  kindLabel,
  questionImageUrl,
  questionSchema,
  questionValues,
  splitGap,
  toInput,
  type Question,
  type QuestionFormValues,
} from "@/lib/cbt/questions";
import { archiveQuestion, createQuestion, refreshBank, updateQuestion } from "@/lib/cbt/actions";
import { runAction } from "@/lib/client/action";
import { useFormDraft } from "@/lib/drafts/use-form-draft";
import { Button, Card, Field, Input, Select, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { ConfirmDialog, FormDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";
import { DraftBanner } from "@/components/drafts/draft-banner";
import { QuestionPicture, sendQuestionPicture } from "./question-picture";
import { QuestionPreview } from "./question-preview";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

/** API field names a 400 can point at. */
const serverFields = ["text", "options", "trueFalseAnswer", "acceptedAnswers", "markingGuide", "topic"] as const;
type Values = QuestionFormValues;

export type BankScope = { subjectId: string; classLevelId: string; subject: string; classLevel: string };

/**
 * E16.1: one form for every kind of question. Options (with correct toggles, reorder), true/false,
 * accepted-answer chips with a gap preview, marking guide, topic, difficulty and a picture. Preview as
 * student hides every key. New questions can "Save and add another" for quick entry.
 */
export function QuestionEditor({ scope, question, topics }: { scope: BankScope; question: Question | null; topics: string[] }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [problem, setProblem] = useState<Problem | null>(null);
  const [answerText, setAnswerText] = useState("");
  const [answerError, setAnswerError] = useState<string | null>(null);
  const [confirmArchive, setConfirmArchive] = useState(false);
  const [picture, setPicture] = useState<{ src: string | null; pending: File | null }>(() => ({
    src: question?.hasImage ? questionImageUrl(question.id, question.updatedAt ?? question.createdAt) : null,
    pending: null,
  }));
  const textRef = useRef<HTMLTextAreaElement | null>(null);
  const form = useForm<Values>({ resolver: zodResolver(questionSchema), defaultValues: question ? questionValues(question) : emptyValues() });
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    getValues,
    reset,
    control,
    formState: { errors, isSubmitting, isDirty },
  } = form;
  const options = useFieldArray({ control, name: "options", keyName: "key" });
  const draft = useFormDraft(form, { scope: ["cbt-question", question?.id ?? `new-${scope.subjectId}-${scope.classLevelId}`] });
  const values = useWatch({ control }) as Values;
  const kind = values.kind;
  const answers = values.acceptedAnswers ?? [];

  // Blob previews are ours to free.
  useEffect(() => () => void (picture.src?.startsWith("blob:") && URL.revokeObjectURL(picture.src)), [picture.src]);

  const applyServerErrors = (p: Problem) => {
    if (p.status !== 400 || !p.errors) return false;
    let applied = false;
    for (const [key, msgs] of Object.entries(p.errors)) {
      const name = key.charAt(0).toLowerCase() + key.slice(1);
      if ((serverFields as readonly string[]).includes(name)) {
        setError(name as FieldPath<Values>, { type: "server", message: msgs.join(" ") }, { shouldFocus: !applied });
        applied = true;
      }
    }
    return applied;
  };

  const save = (andAnother: boolean) =>
    handleSubmit(async (v) => {
      setProblem(null);
      const body = toInput(v, scope);
      const r = await runAction(() => (question ? updateQuestion(question.id, body) : createQuestion(body)), {
        success: question ? "Question saved" : "Question added",
        onProblem: (p) => (applyServerErrors(p) ? undefined : setProblem(p)),
      });
      if (!r) return;
      draft.clear();
      if (!question && picture.pending) {
        const p = await sendQuestionPicture(r.id, picture.pending);
        if (p) toast.error("The question was saved, but not its picture", { description: "Open the question and add the picture again." });
        await refreshBank();
      }
      if (question) {
        reset(questionValues(r));
        return;
      }
      if (andAnother) {
        // Keep the kind and tags for quick entry of similar questions.
        reset({ ...emptyValues(), kind: v.kind, topic: v.topic, difficulty: v.difficulty, options: hasOptions(v.kind) ? v.options.map(() => blankOption()) : emptyValues().options });
        setPicture({ src: null, pending: null });
        setAnswerText("");
        textRef.current?.focus();
        startTransition(() => router.refresh());
        return;
      }
      router.push(bankHref(scope.subjectId, scope.classLevelId));
    });

  const markCorrect = (index: number, checked: boolean) => {
    const current = getValues("options");
    const next = current.map((o, i) => (kind === "Objective" ? { ...o, isCorrect: i === index } : i === index ? { ...o, isCorrect: checked } : o));
    setValue("options", next, { shouldDirty: true, shouldValidate: Boolean(errors.options) });
  };

  const pushAnswer = () => {
    const r = addAnswer(answers, answerText);
    setAnswerError(r.error);
    if (r.error || r.list === answers) return;
    setValue("acceptedAnswers", r.list, { shouldDirty: true, shouldValidate: Boolean(errors.acceptedAnswers) });
    setAnswerText("");
  };

  const insertGap = () => {
    const el = textRef.current;
    const text = getValues("text");
    const at = el?.selectionStart ?? text.length;
    const end = el?.selectionEnd ?? at;
    const next = `${text.slice(0, at)}___${text.slice(end)}`;
    setValue("text", next, { shouldDirty: true, shouldValidate: Boolean(errors.text) });
    requestAnimationFrame(() => {
      el?.focus();
      el?.setSelectionRange(at + 3, at + 3);
    });
  };

  const textField = register("text");
  const optionsError = errors.options?.message ?? errors.options?.root?.message;
  const gap = kind === "FillInTheGap" ? splitGap(values.text ?? "") : null;
  const section = (title: string, children: React.ReactNode, hint?: string) => (
    <Card className="p-5">
      <h2 className="font-display text-lg font-semibold">{title}</h2>
      {hint && <p className="mt-0.5 text-sm text-muted">{hint}</p>}
      <div className="mt-4 space-y-4">{children}</div>
    </Card>
  );

  return (
    <form noValidate onSubmit={(e) => void save(false)(e)} className="space-y-5 pb-4">
      <DraftBanner draft={draft} onDiscard={draft.discard} />
      {problem && (
        <Alert tone="danger" title="Couldn't save the question">
          {problemMessage(problem)}
        </Alert>
      )}

      {section(
        "Question",
        <>
          <Field id="q-kind" label="Kind" hint={kindHint[kind]}>
            <Select {...describedBy("q-kind", undefined, true)} {...register("kind")}>
              {KINDS.map((k) => (
                <option key={k} value={k}>
                  {kindLabel[k]}
                </option>
              ))}
            </Select>
          </Field>
          <Field
            id="q-text"
            label={kind === "TrueFalse" ? "Statement" : kind === "FillInTheGap" ? "Sentence" : "Question"}
            error={errors.text?.message}
            hint={kind === "FillInTheGap" ? "Type ___ (three underscores) where the gap is. One gap per question." : undefined}
          >
            <textarea
              {...describedBy("q-text", errors.text?.message, kind === "FillInTheGap")}
              {...textField}
              ref={(el) => {
                textField.ref(el);
                textRef.current = el;
              }}
              rows={4}
              className={textareaClass}
            />
          </Field>
          {kind === "FillInTheGap" && (
            <div className="flex flex-wrap items-center gap-3">
              <Button type="button" size="sm" variant="secondary" onClick={insertGap} disabled={gapCount(values.text ?? "") >= 1}>
                Insert gap
              </Button>
              <p className="text-sm text-muted" aria-live="polite">
                {gap ? (
                  <>
                    Students see: {gap[0]}
                    <span className="mx-0.5 inline-block w-16 border-b-2 border-text align-baseline" aria-label="gap" />
                    {gap[1]}
                  </>
                ) : gapCount(values.text ?? "") > 1 ? (
                  "Only one gap per question."
                ) : (
                  "No gap yet."
                )}
              </p>
            </div>
          )}
        </>,
      )}

      {hasOptions(kind) &&
        section(
          "Options",
          <>
            <fieldset aria-describedby={optionsError ? "q-options-error" : undefined}>
              <legend className="sr-only">Options; mark the correct {kind === "Objective" ? "one" : "ones"}</legend>
              <ol className="space-y-2">
                {options.fields.map((f, i) => {
                  const letter = String.fromCharCode(65 + i);
                  const correct = values.options?.[i]?.isCorrect ?? false;
                  return (
                    <li key={f.key} className={cn("flex items-center gap-2 rounded-input border p-2", correct ? "border-success bg-success/5" : "border-border")}>
                      <label className="flex min-h-11 min-w-11 cursor-pointer items-center justify-center gap-1.5 text-sm">
                        <input
                          type={kind === "Objective" ? "radio" : "checkbox"}
                          name="q-correct"
                          checked={correct}
                          onChange={(e) => markCorrect(i, e.target.checked)}
                          className="size-4 accent-[var(--success)]"
                          aria-label={`Option ${letter} is correct`}
                        />
                        <span className="font-medium text-muted" aria-hidden>
                          {letter}
                        </span>
                      </label>
                      <Input {...register(`options.${i}.text`)} aria-label={`Option ${letter}`} autoComplete="off" className="min-w-0 flex-1" />
                      <span className="flex shrink-0">
                        <Button type="button" size="sm" variant="ghost" aria-label={`Move option ${letter} up`} disabled={i === 0} onClick={() => options.move(i, i - 1)}>
                          <ArrowUp className="size-4" aria-hidden />
                        </Button>
                        <Button type="button" size="sm" variant="ghost" aria-label={`Move option ${letter} down`} disabled={i === options.fields.length - 1} onClick={() => options.move(i, i + 1)}>
                          <ArrowDown className="size-4" aria-hidden />
                        </Button>
                        <Button type="button" size="sm" variant="ghost" aria-label={`Remove option ${letter}`} disabled={options.fields.length <= 2} onClick={() => options.remove(i)}>
                          <Trash2 className="size-4" aria-hidden />
                        </Button>
                      </span>
                    </li>
                  );
                })}
              </ol>
            </fieldset>
            {optionsError && (
              <p id="q-options-error" className="text-sm text-danger" role="alert">
                {optionsError}
              </p>
            )}
            <Button type="button" size="sm" variant="secondary" disabled={options.fields.length >= MAX_OPTIONS} onClick={() => options.append(blankOption())}>
              <Plus className="size-4" aria-hidden />
              Add option
            </Button>
          </>,
          kind === "Objective" ? "Tick the one correct answer." : "Tick every correct answer.",
        )}

      {kind === "TrueFalse" &&
        section(
          "Answer",
          <fieldset aria-describedby={errors.trueFalseAnswer ? "q-tf-error" : undefined}>
            <legend className="sr-only">The statement is</legend>
            <div className="flex gap-3">
              {(["true", "false"] as const).map((v) => (
                <label key={v} className={cn("flex min-h-11 flex-1 cursor-pointer items-center gap-3 rounded-input border px-3", values.trueFalseAnswer === v ? "border-success bg-success/5" : "border-border")}>
                  <input type="radio" value={v} {...register("trueFalseAnswer")} className="size-4 accent-[var(--success)]" />
                  {v === "true" ? "True" : "False"}
                </label>
              ))}
            </div>
            {errors.trueFalseAnswer && (
              <p id="q-tf-error" className="mt-2 text-sm text-danger" role="alert">
                {errors.trueFalseAnswer.message}
              </p>
            )}
          </fieldset>,
        )}

      {(hasAnswers(kind) || hasGuide(kind)) &&
        section(
          "Marking",
          <>
            {hasAnswers(kind) && (
              <div className="space-y-2">
                <label htmlFor="q-answer" className="block text-sm font-medium">
                  Accepted answers{kind === "ShortAnswer" ? " (optional)" : ""}
                </label>
                <p id="q-answer-hint" className="text-xs text-muted">
                  {kind === "ShortAnswer" ? "Give answers to have the computer mark it; leave empty to mark it yourself. " : ""}Case, extra spaces and a full stop at the end don&apos;t matter.
                </p>
                {answers.length > 0 && (
                  <ul className="flex flex-wrap gap-1.5" aria-label="Answers so far">
                    {answers.map((a) => (
                      <li key={a} className="inline-flex items-center gap-1 rounded-full border border-border bg-surface-2 py-0.5 pr-1 pl-3 text-sm">
                        {a}
                        <button
                          type="button"
                          className="inline-flex size-7 items-center justify-center rounded-full text-muted hover:bg-surface hover:text-text"
                          aria-label={`Remove answer ${a}`}
                          onClick={() => setValue("acceptedAnswers", answers.filter((x) => x !== a), { shouldDirty: true })}
                        >
                          <X className="size-3.5" aria-hidden />
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
                <div className="flex gap-2">
                  <Input
                    id="q-answer"
                    value={answerText}
                    onChange={(e) => {
                      setAnswerText(e.target.value);
                      setAnswerError(null);
                    }}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") {
                        e.preventDefault();
                        pushAnswer();
                      }
                    }}
                    aria-describedby={answerError || errors.acceptedAnswers ? "q-answer-error" : "q-answer-hint"}
                    aria-invalid={answerError || errors.acceptedAnswers ? true : undefined}
                    autoComplete="off"
                    placeholder="Type an answer, then Enter"
                    className="min-w-0 flex-1"
                  />
                  <Button type="button" variant="secondary" onClick={pushAnswer} disabled={answerText.trim() === ""}>
                    Add answer
                  </Button>
                </div>
                {(answerError || errors.acceptedAnswers) && (
                  <p id="q-answer-error" className="text-sm text-danger" role="alert">
                    {answerError ?? errors.acceptedAnswers?.message}
                  </p>
                )}
              </div>
            )}
            {hasGuide(kind) && (
              <Field
                id="q-guide"
                label={kind === "Theory" ? "Marking guide" : "Marking guide (optional)"}
                error={errors.markingGuide?.message}
                hint="The points you expect and how many marks each is worth. Only staff see this."
              >
                <textarea {...describedBy("q-guide", errors.markingGuide?.message, true)} {...register("markingGuide")} rows={4} className={textareaClass} />
              </Field>
            )}
          </>,
        )}

      {section(
        "Tags and picture",
        <>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="q-topic" label="Topic (optional)" error={errors.topic?.message} hint={topics.length > 0 ? "Pick one used before or type a new one." : "e.g. Fractions"}>
              <Input {...describedBy("q-topic", errors.topic?.message, true)} {...register("topic")} list="q-topics" maxLength={MAX_TOPIC} autoComplete="off" />
              <datalist id="q-topics">
                {topics.map((t) => (
                  <option key={t} value={t} />
                ))}
              </datalist>
            </Field>
            <Field id="q-difficulty" label="Difficulty">
              <Select id="q-difficulty" {...register("difficulty")}>
                {DIFFICULTIES.map((d) => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div>
            <p className="mb-2 text-sm font-medium">Picture (optional)</p>
            <QuestionPicture
              questionId={question?.id ?? null}
              src={picture.src}
              onChange={(file) => {
                if (question) {
                  setPicture({ src: file ? questionImageUrl(question.id, `${Date.now()}`) : null, pending: null });
                  startTransition(() => router.refresh());
                } else setPicture({ src: file ? URL.createObjectURL(file) : null, pending: file });
              }}
            />
          </div>
        </>,
      )}

      <div className="sticky bottom-0 z-10 -mx-4 flex flex-wrap items-center gap-2 border-t border-border bg-surface/95 px-4 py-3 backdrop-blur sm:mx-0 sm:rounded-card sm:border sm:shadow-low">
        <FormDialog
          title="Preview as student"
          description="What a student sees. Answers, the marking guide and tags are hidden."
          size="lg"
          trigger={
            <Button type="button" variant="secondary">
              <Eye className="size-4" aria-hidden />
              Preview as student
            </Button>
          }
        >
          {() => <QuestionPreview values={values} imageSrc={picture.src} />}
        </FormDialog>
        {question && (
          <Button type="button" variant="ghost" onClick={() => setConfirmArchive(true)}>
            <Archive className="size-4" aria-hidden />
            Archive
          </Button>
        )}
        <span className="ml-auto flex flex-wrap gap-2">
          {question ? (
            <Link href={bankHref(scope.subjectId, scope.classLevelId)} className="inline-flex min-h-11 items-center px-3 text-sm text-brand hover:underline">
              Back to the bank
            </Link>
          ) : (
            <Button type="button" variant="secondary" loading={isSubmitting} loadingText="Saving…" onClick={(e) => void save(true)(e)}>
              Save and add another
            </Button>
          )}
          <Button type="submit" loading={isSubmitting} loadingText="Saving…" disabled={Boolean(question) && !isDirty}>
            <Save className="size-4" aria-hidden />
            {question ? "Save changes" : "Save question"}
          </Button>
        </span>
      </div>

      {question && (
        <ConfirmDialog
          open={confirmArchive}
          onOpenChange={setConfirmArchive}
          title="Archive this question?"
          confirmLabel="Archive"
          pendingLabel="Archiving…"
          tone="destructive"
          onConfirm={async () => {
            const r = await runAction(() => archiveQuestion(question.id), { success: "Question archived" });
            if (r) {
              draft.clear();
              router.push(bankHref(scope.subjectId, scope.classLevelId));
            }
            return r !== null;
          }}
        >
          <p>It leaves the bank and can&apos;t be added to new tests. Tests that already use it keep it.</p>
        </ConfirmDialog>
      )}
    </form>
  );
}
