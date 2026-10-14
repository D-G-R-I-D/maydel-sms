"use client";

import { useState } from "react";
import { Check, Lock, Save } from "lucide-react";
import { problemMessage } from "@/lib/api/problem";
import { saveFormTeacherComment, savePrincipalComment } from "@/lib/reports/actions";
import { COMMENT_MAX } from "@/lib/reports/rules";
import { runAction } from "@/lib/client/action";
import { useDraft } from "@/lib/drafts/use-draft";
import { DraftBanner } from "@/components/drafts/draft-banner";
import { Button, cn } from "@/components/ui/primitives";
import { toast } from "@/components/ui/toast";

/** One report comment (form teacher's or principal's). Read-only when not allowed or published. */
export function CommentEditor({
  kind,
  studentId,
  termId,
  initial,
  canEdit,
  published,
  author,
}: {
  kind: "formTeacher" | "principal";
  studentId: string;
  termId: string;
  initial: string | null;
  canEdit: boolean;
  published: boolean;
  author?: string | null;
}) {
  const [value, setValue] = useState(initial ?? "");
  const [saved, setSaved] = useState(initial ?? "");
  const [busy, setBusy] = useState(false);
  const label = kind === "formTeacher" ? "Form teacher's comment" : "Principal's comment";
  const id = `comment-${kind}`;
  const dirty = value.trim() !== saved.trim();
  const tooLong = value.length > COMMENT_MAX;
  const draft = useDraft({ scope: ["comment", kind, studentId, termId], value, dirty, server: saved, editable: canEdit && !published, onRestore: setValue });

  if (!canEdit || published) {
    return (
      <div>
        <p className="mb-1 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-muted">
          {label}
          {author && <span className="font-normal normal-case tracking-normal">· {author}</span>}
          {published && canEdit && (
            <span className="inline-flex items-center gap-1 font-normal normal-case tracking-normal" title="Published – ask an admin to unpublish">
              <Lock className="size-3" aria-hidden />
              Published – ask an admin to unpublish
            </span>
          )}
        </p>
        <p className={cn("min-h-12 whitespace-pre-line rounded-input border border-border px-3 py-2 text-sm", !initial && "italic text-muted")}>{initial || "No comment yet."}</p>
      </div>
    );
  }

  const save = async () => {
    setBusy(true);
    const card = await runAction(() => (kind === "formTeacher" ? saveFormTeacherComment(studentId, termId, value) : savePrincipalComment(studentId, termId, value)), {
      success: `${label} saved`,
      onProblem: (p) => p.status > 0 && p.status < 500 && p.status !== 429 && toast.error("Comment not saved", { description: problemMessage(p) }),
    });
    setBusy(false);
    if (card) {
      const next = (kind === "formTeacher" ? card.formTeacherComment : card.principalComment) ?? "";
      setSaved(next);
      setValue(next);
      draft.clear();
    }
  };

  return (
    <div>
      <label htmlFor={id} className="mb-1 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-muted">
        {label}
        {author && <span className="font-normal normal-case tracking-normal">· {author}</span>}
      </label>
      <DraftBanner
        draft={draft}
        onDiscard={() => {
          draft.clear();
          setValue(saved);
        }}
        className="mb-2"
      />
      <textarea
        id={id}
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={(e) => {
          if ((e.ctrlKey || e.metaKey) && e.key === "Enter" && dirty && !tooLong) void save();
        }}
        rows={3}
        aria-invalid={tooLong ? true : undefined}
        placeholder={kind === "formTeacher" ? "e.g. Attentive and hardworking. Keep it up." : "e.g. A good term. Promoted on trial."}
        className={cn(
          "block w-full rounded-input border bg-surface px-3 py-2 text-base text-text placeholder:text-muted/70 focus-visible:outline-2 focus-visible:outline-brand sm:text-sm",
          tooLong ? "border-danger" : "border-border",
        )}
      />
      <div className="mt-1.5 flex items-center gap-3">
        <span className={cn("tabular text-xs", tooLong ? "text-danger" : "text-muted")}>
          {value.length}/{COMMENT_MAX}
        </span>
        <span className="flex-1 text-xs text-muted" aria-live="polite">
          {!dirty && saved && (
            <span className="inline-flex items-center gap-1 text-success">
              <Check className="size-3.5" aria-hidden />
              Saved
            </span>
          )}
        </span>
        <Button size="sm" onClick={save} loading={busy} loadingText="Saving…" disabled={!dirty || tooLong}>
          <Save className="size-3.5" aria-hidden />
          Save
        </Button>
      </div>
    </div>
  );
}
