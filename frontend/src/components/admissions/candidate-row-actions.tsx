"use client";

import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
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

const surname = (name: string) => name.trim().split(/s+/).at(-1) ?? name;

/**
 * Prints a clean attendance sheet. The site's print CSS shows only a body-level `.print-root`, so the
 * sheet is portalled there while printing: surname order, blank Signature and Score columns.
 */
export function PrintButton({ title, subtitle, candidates }: { title: string; subtitle?: string; candidates: ExamCandidate[] }) {
  const [printing, setPrinting] = useState(false);
  useEffect(() => {
    if (!printing) return;
    const done = () => setPrinting(false);
    window.addEventListener("afterprint", done, { once: true });
    window.print();
    return () => window.removeEventListener("afterprint", done);
  }, [printing]);
  const rows = [...candidates].sort((a, b) => surname(a.childName).localeCompare(surname(b.childName)) || a.childName.localeCompare(b.childName));

  return (
    <>
      <Button variant="secondary" onClick={() => setPrinting(true)} className="print:hidden">
        <Printer className="size-4" aria-hidden />
        Print attendance sheet
      </Button>
      {printing &&
        createPortal(
          <div className="print-root hidden" data-testid="attendance-sheet">
            <h1 className="text-xl font-semibold">Entrance exam attendance</h1>
            <p className="mb-4 text-sm">
              {title}
              {subtitle ? ` · ${subtitle}` : ""}
            </p>
            {rows.length === 0 ? (
              <p>No one has booked this sitting yet.</p>
            ) : (
              <table className="w-full border-collapse text-sm">
                <thead>
                  <tr className="border-b border-black text-left">
                    <th className="py-2 pr-2">#</th>
                    <th className="py-2 pr-2">Reference</th>
                    <th className="py-2 pr-2">Child</th>
                    <th className="py-2 pr-2">Class</th>
                    <th className="w-48 py-2 pr-2">Signature</th>
                    <th className="w-20 py-2">Score (%)</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((c, i) => (
                    <tr key={c.applicationId} className="h-10 border-b border-black/40 align-bottom">
                      <td className="py-2 pr-2">{i + 1}</td>
                      <td className="py-2 pr-2">{c.referenceNumber}</td>
                      <td className="py-2 pr-2">{c.childName}</td>
                      <td className="py-2 pr-2">{c.classLevel}</td>
                      <td className="py-2 pr-2" />
                      <td className="py-2" />
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>,
          document.body,
        )}
    </>
  );
}
