"use client";

import { splitGap, type QuestionFormValues } from "@/lib/cbt/questions";
import { Input } from "@/components/ui/primitives";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

/**
 * E16.1 "Preview as student": the question as a student will see it, with no answer keys, marking
 * guide or tags. Answers typed here go nowhere.
 */
export function QuestionPreview({ values, imageSrc }: { values: QuestionFormValues; imageSrc: string | null }) {
  const gap = values.kind === "FillInTheGap" ? splitGap(values.text) : null;
  const name = "preview-answer";

  return (
    <div className="space-y-4" data-testid="student-preview">
      {gap ? (
        <p className="leading-loose whitespace-pre-wrap">
          {gap[0]}
          <Input aria-label="Your answer for the gap" className="mx-1 inline-block w-40 align-baseline" autoComplete="off" />
          {gap[1]}
        </p>
      ) : (
        <p className="whitespace-pre-wrap">{values.text.trim() || <span className="text-muted">The question text appears here.</span>}</p>
      )}
      {/* The private BFF picture or a local blob preview; next/image adds nothing here. */}
      {imageSrc && <img src={imageSrc} alt="Illustration for the question" className="max-h-80 max-w-full rounded-input border border-border object-contain" />}

      {(values.kind === "Objective" || values.kind === "MultiSelect") && (
        <fieldset className="space-y-2">
          <legend className="mb-1 text-sm text-muted">{values.kind === "Objective" ? "Choose one answer." : "Choose every correct answer."}</legend>
          {values.options.map((o, i) => (
            <label key={i} className="flex min-h-11 cursor-pointer items-center gap-3 rounded-input border border-border px-3 py-2 hover:bg-surface-2">
              <input type={values.kind === "Objective" ? "radio" : "checkbox"} name={name} className="size-4 accent-[var(--brand)]" />
              <span className="font-medium text-muted">{String.fromCharCode(65 + i)}.</span>
              <span className="break-words">{o.text || <span className="text-muted">(empty option)</span>}</span>
            </label>
          ))}
        </fieldset>
      )}
      {values.kind === "TrueFalse" && (
        <fieldset className="flex gap-3">
          <legend className="sr-only">True or false</legend>
          {["True", "False"].map((l) => (
            <label key={l} className="flex min-h-11 flex-1 cursor-pointer items-center gap-3 rounded-input border border-border px-3 py-2 hover:bg-surface-2">
              <input type="radio" name={name} className="size-4 accent-[var(--brand)]" />
              {l}
            </label>
          ))}
        </fieldset>
      )}
      {values.kind === "ShortAnswer" && <Input aria-label="Your answer" placeholder="Type your answer" autoComplete="off" />}
      {values.kind === "Theory" && <textarea aria-label="Your answer" rows={6} placeholder="Write your answer" className={textareaClass} />}
    </div>
  );
}
