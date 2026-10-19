import { Check, CircleDot, X } from "lucide-react";
import type { ReviewItem } from "@/lib/cbt/marking";
import { kindLabel, questionImageUrl } from "@/lib/cbt/questions";
import { formatMarks } from "@/lib/cbt/tests";
import { Badge, cn } from "@/components/ui/primitives";

/** E16.4: one answer next to the key or marking guide, for staff marking. */
export function ReviewAnswer({ item, number }: { item: ReviewItem; number: number }) {
  const choice = item.kind === "Objective" || item.kind === "MultiSelect";
  return (
    <div className="space-y-3">
      <p className="flex flex-wrap items-baseline gap-2 text-sm text-muted">
        <span className="font-medium text-text">Question {number}</span>
        <Badge>{kindLabel[item.kind]}</Badge>
        <span className="tabular">
          {formatMarks(item.marks)} {item.marks === 1 ? "mark" : "marks"}
        </span>
      </p>
      <p className="whitespace-pre-wrap">{item.text}</p>
      {/* The private BFF picture; next/image adds nothing here. */}
      {item.hasImage && <img src={questionImageUrl(item.questionId, item.id)} alt={`Illustration for question ${number}`} className="max-h-64 max-w-full rounded-input border border-border object-contain" />}

      {choice && (
        <ul className="space-y-1.5" aria-label="Options">
          {item.options.map((o, i) => (
            <li
              key={o.id}
              className={cn("flex items-center gap-2 rounded-input border px-3 py-2 text-sm", o.isCorrect ? "border-success bg-success/5" : o.chosen ? "border-danger bg-danger/5" : "border-border")}
            >
              <span className="font-medium text-muted">{String.fromCharCode(65 + i)}.</span>
              <span className="min-w-0 flex-1 break-words">{o.text}</span>
              {o.chosen && (
                <span className="inline-flex items-center gap-1 text-xs font-medium">
                  <CircleDot className="size-3.5" aria-hidden />
                  Chosen
                </span>
              )}
              {o.isCorrect ? <Check className="size-4 text-success" aria-label="Correct option" /> : o.chosen ? <X className="size-4 text-danger" aria-label="Wrong option" /> : null}
            </li>
          ))}
        </ul>
      )}
      {item.kind === "TrueFalse" && (
        <p className="text-sm">
          Answer: <span className="font-medium">{item.answerTrueFalse === null ? "No answer" : item.answerTrueFalse ? "True" : "False"}</span> · Correct:{" "}
          <span className="font-medium text-success">{item.correctTrueFalse ? "True" : "False"}</span>
        </p>
      )}
      {!choice && item.kind !== "TrueFalse" && (
        <div className="rounded-input border border-border bg-surface-2 p-3 text-sm">
          <p className="mb-1 text-xs font-medium text-muted">Student&apos;s answer</p>
          <p className="whitespace-pre-wrap break-words">{item.answered && item.answerText ? item.answerText : <span className="text-muted">No answer</span>}</p>
        </div>
      )}
      {item.acceptedAnswers.length > 0 && (
        <p className="text-sm">
          <span className="text-muted">Accepted: </span>
          {item.acceptedAnswers.join(" · ")}
        </p>
      )}
      {item.markingGuide && (
        <div className="rounded-input border border-brand/30 bg-brand/5 p-3 text-sm">
          <p className="mb-1 text-xs font-medium text-brand">Marking guide</p>
          <p className="whitespace-pre-wrap">{item.markingGuide}</p>
        </div>
      )}
      {item.autoScore !== null && (
        <p className="tabular text-xs text-muted">
          Computer&apos;s mark: {formatMarks(item.autoScore)}
          {item.teacherScore !== null && item.teacherScore !== item.autoScore ? ` · changed to ${formatMarks(item.teacherScore)}` : ""}
        </p>
      )}
    </div>
  );
}
