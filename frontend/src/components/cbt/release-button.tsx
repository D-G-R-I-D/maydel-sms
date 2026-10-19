"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Send } from "lucide-react";
import { releaseResults } from "@/lib/cbt/actions";
import { runAction } from "@/lib/client/action";
import { Button } from "@/components/ui/primitives";
import { ConfirmDialog } from "@/components/ui/dialog";

/** E16.4: release results to students and parents. Disabled with the reason when it can't go yet. */
export function ReleaseButton({ testId, disabledReason, students }: { testId: string; disabledReason: string | null; students: number }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button onClick={() => setOpen(true)} disabled={disabledReason !== null}>
        <Send className="size-4" aria-hidden />
        Release results
      </Button>
      <ConfirmDialog
        open={open}
        onOpenChange={setOpen}
        title="Release the results?"
        confirmLabel="Release"
        pendingLabel="Releasing…"
        onConfirm={async () => {
          const r = await runAction(() => releaseResults(testId), { success: "Results released" });
          if (r) startTransition(() => router.refresh());
          return r !== null;
        }}
      >
        <p>
          {students} {students === 1 ? "student" : "students"} and their parents will see the score, the marks for each question and your comments. Marks can&apos;t be changed after this.
        </p>
      </ConfirmDialog>
    </>
  );
}
