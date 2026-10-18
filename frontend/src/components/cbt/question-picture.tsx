"use client";

import { useRef, useState } from "react";
import { ImagePlus, Trash2 } from "lucide-react";
import { isProblem, problemMessage, toProblem, type Problem } from "@/lib/api/problem";
import { unreachableProblem } from "@/lib/client/unreachable";
import { IMAGE_ACCEPT, imageFileError } from "@/lib/cbt/questions";
import { Button } from "@/components/ui/primitives";
import { toast } from "@/components/ui/toast";

/** PUT or DELETE a question's picture through the BFF. Returns a problem, or null on success. */
export async function sendQuestionPicture(id: string, file: File | null): Promise<Problem | null> {
  let body: FormData | undefined;
  if (file) {
    body = new FormData();
    body.append("file", file);
  }
  try {
    const res = await fetch(`/api/media/cbt/questions/${id}/image`, { method: file ? "PUT" : "DELETE", body, credentials: "same-origin" });
    if (res.ok) return null;
    const json: unknown = await res.json().catch(() => null);
    return isProblem(json) ? json : toProblem(res.status, json);
  } catch {
    return unreachableProblem();
  }
}

const describe = (p: Problem) => (p.errors ? Object.values(p.errors).flat().join(" ") : problemMessage(p));

/**
 * The question's one picture. For a saved question (`questionId`) changes upload straight away; for a
 * new one the file is held (`pending`) and uploaded after the question is created.
 */
export function QuestionPicture({
  questionId,
  src,
  onChange,
}: {
  questionId: string | null;
  /** What to show now: the saved picture's URL or a local blob URL; null for none. */
  src: string | null;
  /** A new local file (null = removed). For a saved question, called only after the upload succeeds. */
  onChange: (file: File | null) => void;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);

  const apply = async (file: File | null) => {
    if (file) {
      const err = imageFileError(file);
      if (err) {
        toast.error("Can't use this picture", { description: err });
        return;
      }
    }
    if (questionId) {
      setBusy(true);
      const p = await sendQuestionPicture(questionId, file);
      setBusy(false);
      if (p) {
        toast.error(file ? "Couldn't upload the picture" : "Couldn't remove the picture", { description: describe(p) });
        return;
      }
      toast.success(file ? "Picture saved" : "Picture removed");
    }
    onChange(file);
  };

  return (
    <div className="space-y-3">
      {src ? (
        // The private BFF picture or a local blob preview; next/image adds nothing here.
        <img src={src} alt="Illustration for the question" className="max-h-64 max-w-full rounded-input border border-border object-contain" />
      ) : (
        <p className="text-sm text-muted">No picture. Add a diagram, map or photo if the question needs one (JPEG, PNG or WebP, up to 12 MB).</p>
      )}
      <div className="flex flex-wrap gap-2">
        <input
          ref={input}
          type="file"
          accept={IMAGE_ACCEPT}
          className="sr-only"
          tabIndex={-1}
          aria-hidden
          onChange={(e) => {
            const f = e.target.files?.[0];
            e.target.value = "";
            if (f) void apply(f);
          }}
        />
        <Button type="button" size="sm" variant="secondary" loading={busy} loadingText="Saving…" onClick={() => input.current?.click()}>
          <ImagePlus className="size-4" aria-hidden />
          {src ? "Replace picture" : "Add picture"}
        </Button>
        {src && (
          <Button type="button" size="sm" variant="ghost" disabled={busy} onClick={() => void apply(null)}>
            <Trash2 className="size-4" aria-hidden />
            Remove picture
          </Button>
        )}
      </div>
    </div>
  );
}
