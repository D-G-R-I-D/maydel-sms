"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Check } from "lucide-react";
import { problemMessage } from "@/lib/api/problem";
import { markAnswer } from "@/lib/cbt/actions";
import { markError } from "@/lib/cbt/marking";
import { formatMarks } from "@/lib/cbt/tests";
import { runAction } from "@/lib/client/action";
import { Input, cn } from "@/components/ui/primitives";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

/**
 * E16.4: the mark (and optional comment) for one answer. Saves when you leave the field, so you can
 * tab through a class quickly. A mark here also overrides the computer's.
 */
export function MarkBox({
  attemptId,
  itemId,
  marks,
  score,
  autoScore,
  comment,
  canMark,
  label,
}: {
  attemptId: string;
  itemId: string;
  marks: number;
  /** The mark that counts now (the teacher's if any, else the computer's). */
  score: number | null;
  autoScore: number | null;
  comment: string | null;
  canMark: boolean;
  /** e.g. "question 3" or "Ada Obi", for the field labels. */
  label: string;
}) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [value, setValue] = useState(score === null ? "" : String(score));
  const [note, setNote] = useState(comment ?? "");
  const [saved, setSaved] = useState({ value: score === null ? "" : String(score), note: comment ?? "" });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const id = `mark-${attemptId}-${itemId}`;

  const save = async () => {
    if (!canMark || busy) return;
    if (value === saved.value && note.trim() === saved.note.trim()) return;
    const err = markError(value, marks);
    if (err) {
      // An untouched, never-marked box isn't an error until a mark is typed.
      if (!(value.trim() === "" && saved.value === "")) setError(err);
      return;
    }
    setError(null);
    setBusy(true);
    const r = await runAction(() => markAnswer(attemptId, itemId, Number(value), note.trim() === "" ? null : note.trim()), {
      success: `Mark saved for ${label}`,
      onProblem: (p) => setError(p.errors ? Object.values(p.errors).flat().join(" ") : problemMessage(p)),
    });
    setBusy(false);
    if (r) {
      setSaved({ value, note });
      // Totals and "to mark" counts live on the server-rendered page around this box.
      startTransition(() => router.refresh());
    }
  };

  if (!canMark) {
    return (
      <div className="text-sm">
        <p className="tabular font-medium">
          {score === null ? "Not marked" : `${formatMarks(score)} / ${formatMarks(marks)}`}
        </p>
        {comment && <p className="mt-1 text-muted">{comment}</p>}
      </div>
    );
  }

  const isSaved = value !== "" && value === saved.value && note.trim() === saved.note.trim();
  return (
    <div className="space-y-2">
      <div className="flex items-end gap-2">
        <div>
          <label htmlFor={id} className="mb-1 block text-xs font-medium">
            Mark for {label}
          </label>
          <span className="flex items-center gap-1.5">
            <Input
              id={id}
              value={value}
              onChange={(e) => {
                setValue(e.target.value);
                setError(null);
              }}
              onBlur={() => void save()}
              onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), void save())}
              inputMode="decimal"
              className={cn("tabular w-20", error && "border-danger")}
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? `${id}-error` : undefined}
            />
            <span className="tabular text-sm text-muted">/ {formatMarks(marks)}</span>
          </span>
        </div>
        <span className="pb-2.5 text-xs text-muted" aria-live="polite">
          {busy ? "Saving…" : isSaved ? (
            <span className="inline-flex items-center gap-1 text-success">
              <Check className="size-3.5" aria-hidden />
              Saved
            </span>
          ) : autoScore !== null && saved.value === String(autoScore) ? (
            "Computer's mark"
          ) : null}
        </span>
      </div>
      <textarea aria-label={`Comment for ${label} (optional, shown after release)`} value={note} onChange={(e) => setNote(e.target.value)} onBlur={() => void save()} rows={2} maxLength={1000} placeholder="Comment (optional)" className={textareaClass} />
      {error && (
        <p id={`${id}-error`} className="text-sm text-danger" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
