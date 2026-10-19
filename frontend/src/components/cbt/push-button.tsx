"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { TableProperties } from "lucide-react";
import { pushToGradebook } from "@/lib/cbt/actions";
import { pushStateLabel, type GradebookRow } from "@/lib/cbt/marking";
import { runAction } from "@/lib/client/action";
import { Button } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";

/** E16.5: write the ready scores to the grade sheet, then list anyone skipped and why. */
export function PushButton({ testId, ready, component }: { testId: string; ready: number; component: string }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [open, setOpen] = useState(false);
  const [skipped, setSkipped] = useState<GradebookRow[] | null>(null);

  return (
    <div className="space-y-3">
      <Button onClick={() => setOpen(true)} disabled={ready === 0}>
        <TableProperties className="size-4" aria-hidden />
        Send to grade sheet
      </Button>
      {skipped && skipped.length > 0 && (
        <Alert tone="warning" title={`${skipped.length} skipped`}>
          <ul className="mt-1 space-y-0.5">
            {skipped.map((r) => (
              <li key={r.studentId}>
                {r.name}: {pushStateLabel[r.state].toLowerCase()}
              </li>
            ))}
          </ul>
        </Alert>
      )}
      <ConfirmDialog
        open={open}
        onOpenChange={setOpen}
        title={`Write ${ready} ${ready === 1 ? "score" : "scores"} to ${component}?`}
        confirmLabel="Send"
        pendingLabel="Sending…"
        onConfirm={async () => {
          const r = await runAction(() => pushToGradebook(testId, null), { success: (x) => `${x.pushed} ${x.pushed === 1 ? "score" : "scores"} sent to the grade sheet` });
          if (r) {
            setSkipped(r.skipped);
            startTransition(() => router.refresh());
          }
          return r !== null;
        }}
      >
        <p>Only the {component} column changes; totals and grades are recalculated. Students with a published report keep theirs.</p>
      </ConfirmDialog>
    </div>
  );
}
