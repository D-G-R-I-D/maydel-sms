"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { CheckCheck, Undo2 } from "lucide-react";
import { runAction } from "@/lib/client/action";
import { reviewHandIn } from "@/lib/homework/actions";
import type { Submission } from "@/lib/homework/homework";
import { Button } from "@/components/ui/primitives";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

/** Mark a hand-in Seen, or Return it with feedback (the student is notified and can hand in again). */
export function ReviewHandIn({ homeworkId, submission, studentName }: { homeworkId: string; submission: Submission; studentName: string }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [feedback, setFeedback] = useState(submission.feedback ?? "");
  const [busy, setBusy] = useState<"Seen" | "Returned" | null>(null);
  const id = `fb-${submission.id}`;
  const tooLong = feedback.length > 2000;

  const review = async (status: "Seen" | "Returned") => {
    setBusy(status);
    const r = await runAction(() => reviewHandIn(homeworkId, submission.id, status, feedback.trim() || null), {
      success: status === "Seen" ? `Marked ${studentName}'s as seen` : `Returned to ${studentName} with feedback`,
    });
    setBusy(null);
    if (r) startTransition(() => router.refresh());
  };

  return (
    <div className="space-y-2">
      <label htmlFor={id} className="block text-sm font-medium">
        Feedback (optional when marking seen)
      </label>
      <textarea id={id} value={feedback} onChange={(e) => setFeedback(e.target.value)} rows={3} className={textareaClass} aria-invalid={tooLong || undefined} />
      {tooLong && <p className="text-xs text-danger">2000 characters at most.</p>}
      <div className="flex flex-wrap gap-2">
        <Button size="sm" loading={busy === "Seen"} loadingText="Saving…" disabled={tooLong} onClick={() => void review("Seen")}>
          <CheckCheck className="size-4" aria-hidden />
          Mark seen
        </Button>
        <Button size="sm" variant="secondary" loading={busy === "Returned"} loadingText="Saving…" disabled={tooLong || !feedback.trim()} onClick={() => void review("Returned")}>
          <Undo2 className="size-4" aria-hidden />
          Return with feedback
        </Button>
      </div>
    </div>
  );
}
