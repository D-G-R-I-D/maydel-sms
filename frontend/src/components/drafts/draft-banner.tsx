"use client";

import { History, TriangleAlert } from "lucide-react";
import { draftTime } from "@/lib/drafts/rules";
import type { DraftState } from "@/lib/drafts/use-draft";
import { Button, cn } from "@/components/ui/primitives";

/** "Restored your unsaved changes from 14:05" with Discard; warns when the saved copy changed since. */
export function DraftBanner({ draft, onDiscard, className }: { draft: DraftState; onDiscard: () => void; className?: string }) {
  if (draft.restoredAt === null) return null;
  const Icon = draft.changedUnderneath ? TriangleAlert : History;
  return (
    <div
      role="status"
      className={cn(
        "animate-rise flex flex-wrap items-center gap-x-3 gap-y-1 rounded-input border px-3 py-2 text-sm",
        draft.changedUnderneath ? "border-warning/40 bg-warning/10" : "border-brand/25 bg-brand/5",
        className,
      )}
    >
      <Icon className={cn("size-4 shrink-0", draft.changedUnderneath ? "text-warning" : "text-brand")} aria-hidden />
      <span className="min-w-0 flex-1">
        Restored your unsaved changes from {draftTime(draft.restoredAt)}.
        {draft.changedUnderneath && " This was changed elsewhere since; check before saving."}
      </span>
      <Button size="sm" variant="ghost" onClick={onDiscard}>
        Discard
      </Button>
    </div>
  );
}
