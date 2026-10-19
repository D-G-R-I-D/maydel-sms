"use client";

import { Plus, Trash2 } from "lucide-react";
import { DIFFICULTIES, KINDS, MAX_OPTIONS, blankOption, hasAnswers, hasGuide, hasOptions, kindLabel, type Difficulty, type QuestionFormValues, type QuestionKind } from "@/lib/cbt/questions";
import { Button, Input, Select, cn } from "@/components/ui/primitives";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

/**
 * E16.8: a compact, controlled editor for one AI draft: the same fields and rules as the bank's
 * question editor (questionSchema), laid out to review many at once.
 */
export function DraftEditor({ id, values, onChange, topics }: { id: string; values: QuestionFormValues; onChange: (v: QuestionFormValues) => void; topics: string[] }) {
  const set = (patch: Partial<QuestionFormValues>) => onChange({ ...values, ...patch });
  const options = values.options;
  const label = (suffix: string) => `${id}-${suffix}`;

  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-[12rem_1fr]">
        <div className="space-y-1">
          <label htmlFor={label("kind")} className="block text-xs font-medium">
            Kind
          </label>
          <Select id={label("kind")} value={values.kind} onChange={(e) => set({ kind: e.target.value as QuestionKind })}>
            {KINDS.map((k) => (
              <option key={k} value={k}>
                {kindLabel[k]}
              </option>
            ))}
          </Select>
        </div>
        <div className="space-y-1">
          <label htmlFor={label("text")} className="block text-xs font-medium">
            {values.kind === "FillInTheGap" ? "Sentence (___ marks the gap)" : values.kind === "TrueFalse" ? "Statement" : "Question"}
          </label>
          <textarea id={label("text")} value={values.text} onChange={(e) => set({ text: e.target.value })} rows={2} className={textareaClass} />
        </div>
      </div>

      {hasOptions(values.kind) && (
        <fieldset className="space-y-1.5">
          <legend className="mb-1 text-xs font-medium">Options; tick the correct {values.kind === "Objective" ? "one" : "ones"}</legend>
          {options.map((o, i) => {
            const letter = String.fromCharCode(65 + i);
            return (
              <div key={i} className={cn("flex items-center gap-2 rounded-input border p-1.5", o.isCorrect ? "border-success bg-success/5" : "border-border")}>
                <input
                  type={values.kind === "Objective" ? "radio" : "checkbox"}
                  name={label("correct")}
                  checked={o.isCorrect ?? false}
                  onChange={(e) => set({ options: options.map((x, j) => (values.kind === "Objective" ? { ...x, isCorrect: j === i } : j === i ? { ...x, isCorrect: e.target.checked } : x)) })}
                  aria-label={`Option ${letter} is correct`}
                  className="ml-1 size-4 accent-[var(--success)]"
                />
                <Input value={o.text ?? ""} onChange={(e) => set({ options: options.map((x, j) => (j === i ? { ...x, text: e.target.value } : x)) })} aria-label={`Option ${letter}`} className="min-w-0 flex-1" />
                <Button type="button" size="sm" variant="ghost" aria-label={`Remove option ${letter}`} disabled={options.length <= 2} onClick={() => set({ options: options.filter((_, j) => j !== i) })}>
                  <Trash2 className="size-4" aria-hidden />
                </Button>
              </div>
            );
          })}
          <Button type="button" size="sm" variant="ghost" disabled={options.length >= MAX_OPTIONS} onClick={() => set({ options: [...options, blankOption()] })}>
            <Plus className="size-4" aria-hidden />
            Add option
          </Button>
        </fieldset>
      )}

      {values.kind === "TrueFalse" && (
        <fieldset className="flex gap-2">
          <legend className="mb-1 text-xs font-medium">The statement is</legend>
          {(["true", "false"] as const).map((v) => (
            <label key={v} className={cn("flex min-h-10 flex-1 cursor-pointer items-center gap-2 rounded-input border px-3 text-sm", values.trueFalseAnswer === v ? "border-success bg-success/5" : "border-border")}>
              <input type="radio" name={label("tf")} checked={values.trueFalseAnswer === v} onChange={() => set({ trueFalseAnswer: v })} className="size-4 accent-[var(--success)]" />
              {v === "true" ? "True" : "False"}
            </label>
          ))}
        </fieldset>
      )}

      {(hasAnswers(values.kind) || hasGuide(values.kind)) && (
        <div className="grid gap-3 sm:grid-cols-2">
          {hasAnswers(values.kind) && (
            <div className="space-y-1">
              <label htmlFor={label("answers")} className="block text-xs font-medium">
                Accepted answers, one per line{values.kind === "ShortAnswer" ? " (optional)" : ""}
              </label>
              <textarea
                id={label("answers")}
                value={values.acceptedAnswers.join("\n")}
                onChange={(e) => set({ acceptedAnswers: e.target.value.split("\n").map((a) => a.trimStart()).filter((a, i, all) => a !== "" || i === all.length - 1) })}
                rows={3}
                className={textareaClass}
              />
            </div>
          )}
          {hasGuide(values.kind) && (
            <div className="space-y-1">
              <label htmlFor={label("guide")} className="block text-xs font-medium">
                Marking guide{values.kind === "ShortAnswer" ? " (optional)" : ""}
              </label>
              <textarea id={label("guide")} value={values.markingGuide} onChange={(e) => set({ markingGuide: e.target.value })} rows={3} className={textareaClass} />
            </div>
          )}
        </div>
      )}

      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1">
          <label htmlFor={label("topic")} className="block text-xs font-medium">
            Topic
          </label>
          <Input id={label("topic")} value={values.topic} onChange={(e) => set({ topic: e.target.value })} list={label("topics")} maxLength={80} autoComplete="off" />
          <datalist id={label("topics")}>
            {topics.map((t) => (
              <option key={t} value={t} />
            ))}
          </datalist>
        </div>
        <div className="space-y-1">
          <label htmlFor={label("difficulty")} className="block text-xs font-medium">
            Difficulty
          </label>
          <Select id={label("difficulty")} value={values.difficulty} onChange={(e) => set({ difficulty: e.target.value as Difficulty })}>
            {DIFFICULTIES.map((d) => (
              <option key={d} value={d}>
                {d}
              </option>
            ))}
          </Select>
        </div>
      </div>
    </div>
  );
}
