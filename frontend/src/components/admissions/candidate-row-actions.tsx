"use client";

import { useState } from "react";
import { Check, Printer, UserX } from "lucide-react";
import { problemMessage } from "@/lib/api/problem";
import type { ExamCandidate } from "@/lib/admissions/exam";
import { scoreError } from "@/lib/admissions/exam";
import { markExamAbsent, recordExamScore } from "@/lib/admissions/actions";
import { runAction } from "@/lib/client/action";
import { Button, Input } from "@/components/ui/primitives";
import { ConfirmDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";

/** Inline score (0–100) and Absent for one candidate (E11.6, admin or teacher). */
export function CandidateScore({ candidate }: { candidate: ExamCandidate }) {
  const [value, setValue] = useState(candidate.score === null || candidate.score === undefined ? "" : String(candidate.score));
  const [saved, setSaved] = useState(value);
  const [busy, setBusy] = useState(false);
  const [absent, setAbsent] = useState(false);
  const error = value !== saved ? scoreError(value) : null;
  const canScore = candidate.status === "ExamBooked" || candidate.status === "Assessed";

  if (!canScore) return <span className="text-sm text-muted">{candidate.status === "Missed" ? "Absent" : "—"}</span>;

  return (
    <div className="flex flex-wrap items-start gap-2 print:hidden">
      <form
        className="flex items-start gap-1.5"
        noValidate
        onSubmit={async (e) => {
          e.preventDefault();
          if (scoreError(value)) return;
          setBusy(true);
          const r = await runAction(() => recordExamScore(candidate.applicationId, Number(value)), {
            success: `Score saved for ${candidate.childName}`,
            onProblem: (p) => p.status > 0 && p.status < 500 && p.status !== 429 && toast.error("Couldn't save the score", { description: p.errors ? Object.values(p.errors).flat().join(" ") : problemMessage(p) }),
          });
          setBusy(false);
          if (r) setSaved(value);
        }}
      >
        <div>
          <Input
            aria-label={`Score for ${candidate.childName}`}
            value={value}
            onChange={(e) => setValue(e.target.value)}
            inputMode="decimal"
            className="tabular w-20"
            aria-invalid={error ? true : undefined}
          />
          {error && <p className="mt-1 max-w-40 text-xs text-danger">{error}</p>}
        </div>
        <Button type="submit" size="sm" variant="secondary" loading={busy} loadingText="…" disabled={value === saved || Boolean(error)} aria-label={`Save score for ${candidate.childName}`}>
          <Check className="size-4" aria-hidden />
        </Button>
      </form>
      {candidate.status === "ExamBooked" && (
        <>
          <Button size="sm" variant="ghost" onClick={() => setAbsent(true)}>
            <UserX className="size-4" aria-hidden />
            Absent
          </Button>
          <ConfirmDialog
            open={absent}
            onOpenChange={setAbsent}
            title={`Mark ${candidate.childName} absent?`}
            confirmLabel="Mark absent"
            pendingLabel="Saving…"
            tone="destructive"
            onConfirm={async () => (await runAction(() => markExamAbsent(candidate.applicationId), { success: `${candidate.childName} marked absent` })) !== null}
          >
            <p>The application becomes &ldquo;Missed exam&rdquo;. An administrator can reopen it to give the family a new window.</p>
          </ConfirmDialog>
        </>
      )}
    </div>
  );
}

export function PrintButton() {
  return (
    <Button variant="secondary" onClick={() => window.print()} className="print:hidden">
      <Printer className="size-4" aria-hidden />
      Print attendance sheet
    </Button>
  );
}
