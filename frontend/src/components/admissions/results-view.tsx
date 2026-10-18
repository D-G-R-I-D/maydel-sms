"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { CheckCheck, Download, MailCheck, Send } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { cutOffError, type ExamCandidate, type ExamResults } from "@/lib/admissions/exam";
import { applicationStatusLabel } from "@/lib/admissions/rules";
import { notifyPassed, publishDecisions, setCutOff, setDecisions } from "@/lib/admissions/actions";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Card, Input, buttonClasses, cn } from "@/components/ui/primitives";
import { EmptyState } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";

type Tab = "taken" | "passed" | "failed";
const decidable = (c: ExamCandidate) => c.status === "Assessed";
const showError = (title: string) => (p: Problem) =>
  p.status > 0 && p.status < 500 && p.status !== 429 && toast.error(title, { description: p.errors ? Object.values(p.errors).flat().join(" ") : problemMessage(p) });

/**
 * E11.7–E11.8 (admin): the cut-off, who took the exam / passed / failed, offer ticks (draft until
 * published), "Notify passed" and "Publish decisions". Families see nothing until publish.
 */
export function ResultsView({ sessionId, initial, csvHref }: { sessionId: string; initial: ExamResults; csvHref: string }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [results, setResults] = useState(initial);
  const [tab, setTab] = useState<Tab>(initial.cutOffMark === null ? "taken" : "passed");
  const [cutOff, setCutOffText] = useState(initial.cutOffMark === null ? "" : String(initial.cutOffMark));
  const [busy, setBusy] = useState<string | null>(null);
  const [confirmNotify, setConfirmNotify] = useState(false);
  const [confirmPublish, setConfirmPublish] = useState(false);
  const cutError = cutOffError(cutOff);

  const merge = (updated: ExamCandidate[]) => {
    const byId = new Map(updated.map((c) => [c.applicationId, c]));
    const fix = (list: ExamCandidate[]) => list.map((c) => byId.get(c.applicationId) ?? c);
    setResults((r) => ({ ...r, taken: fix(r.taken), passed: fix(r.passed), failed: fix(r.failed) }));
  };

  const decide = async (ids: string[], decision: "Offer" | "Decline" | null, key: string) => {
    setBusy(key);
    const r = await runAction(() => setDecisions(ids, decision), { success: decision === "Offer" ? (ids.length > 1 ? `${ids.length} ticked for an offer` : "Ticked for an offer") : "Tick removed", onProblem: showError("Couldn't save the decision") });
    setBusy(null);
    if (r) merge(r);
  };

  const list = results[tab];
  const toNotify = results.passed.filter((c) => !c.stageOneNotifiedAt).length;
  const offers = results.taken.filter((c) => c.decision === "Offer" && decidable(c)).length;
  const others = results.taken.filter((c) => c.decision !== "Offer" && decidable(c)).length;
  const passedUnticked = results.passed.filter((c) => decidable(c) && c.decision !== "Offer");

  return (
    <div className="space-y-5">
      <Card className="flex flex-wrap items-end gap-4 p-5">
        <form
          className="flex flex-wrap items-end gap-2"
          noValidate
          onSubmit={async (e) => {
            e.preventDefault();
            if (cutError) return;
            setBusy("cutoff");
            const r = await runAction(() => setCutOff(sessionId, cutOff.trim() === "" ? null : Number(cutOff)), { success: cutOff.trim() === "" ? "Cut-off cleared" : `Cut-off set to ${cutOff}%`, onProblem: showError("Couldn't set the cut-off") });
            setBusy(null);
            if (r) {
              setResults(r);
              if (r.cutOffMark !== null) setTab("passed");
            }
          }}
        >
          <div>
            <label htmlFor="cutoff" className="mb-1.5 block text-sm font-medium">
              Cut-off mark (%)
            </label>
            <Input id="cutoff" value={cutOff} onChange={(e) => setCutOffText(e.target.value)} inputMode="decimal" className="tabular w-28" aria-invalid={cutError ? true : undefined} placeholder="e.g. 50" />
          </div>
          <Button type="submit" variant="secondary" loading={busy === "cutoff"} loadingText="Saving…" disabled={Boolean(cutError) || cutOff === (results.cutOffMark === null ? "" : String(results.cutOffMark))}>
            Apply
          </Button>
          {cutError && <p className="w-full text-sm text-danger">{cutError}</p>}
        </form>
        <dl className="ml-auto flex flex-wrap gap-x-6 gap-y-1 text-sm">
          <div>
            <dt className="text-muted">Awaiting a score</dt>
            <dd className="tabular font-semibold">{results.awaitingScore}</dd>
          </div>
          <div>
            <dt className="text-muted">Missed the exam</dt>
            <dd className="tabular font-semibold">{results.missed}</dd>
          </div>
        </dl>
      </Card>

      <div className="flex flex-wrap items-center gap-2">
        <nav aria-label="Results" className="flex flex-wrap gap-1 rounded-input bg-surface-2 p-1">
          {(
            [
              ["taken", "Took the exam", results.taken.length],
              ["passed", "Passed", results.passed.length],
              ["failed", "Failed", results.failed.length],
            ] as const
          ).map(([key, label, n]) => (
            <button
              key={key}
              type="button"
              aria-pressed={tab === key}
              onClick={() => setTab(key)}
              disabled={key !== "taken" && results.cutOffMark === null}
              className={cn("inline-flex min-h-9 items-center gap-1.5 rounded-[6px] px-3 text-sm transition-colors disabled:opacity-50", tab === key ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
            >
              {label} <span className="tabular text-xs">{n}</span>
            </button>
          ))}
        </nav>
        <div className="ml-auto flex flex-wrap gap-2">
          <a href={csvHref} download className={buttonClasses("secondary", "sm")}>
            <Download className="size-4" aria-hidden />
            Export CSV
          </a>
          <Button size="sm" variant="secondary" disabled={results.cutOffMark === null || toNotify === 0} onClick={() => setConfirmNotify(true)}>
            <MailCheck className="size-4" aria-hidden />
            Notify passed ({toNotify})
          </Button>
          <Button size="sm" disabled={offers + others === 0} onClick={() => setConfirmPublish(true)}>
            <Send className="size-4" aria-hidden />
            Publish decisions
          </Button>
        </div>
      </div>
      {results.cutOffMark === null && <p className="text-sm text-muted">Set a cut-off mark to see who passed and failed.</p>}

      <Card className="overflow-hidden">
        {tab === "passed" && passedUnticked.length > 0 && (
          <div className="flex justify-end border-b border-border px-4 py-2">
            <Button size="sm" variant="ghost" loading={busy === "all"} loadingText="Ticking…" onClick={() => decide(passedUnticked.map((c) => c.applicationId), "Offer", "all")}>
              <CheckCheck className="size-4" aria-hidden />
              Tick all passed ({passedUnticked.length})
            </Button>
          </div>
        )}
        {list.length === 0 ? (
          <EmptyState title={tab === "taken" ? "No scores yet" : tab === "passed" ? "Nobody at or above the cut-off" : "Nobody below the cut-off"}>
            {tab === "taken" ? "Scores entered on the exam sittings appear here." : "Try a different cut-off."}
          </EmptyState>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[44rem] text-sm">
              <thead className="text-left text-xs uppercase tracking-wider text-muted">
                <tr className="border-b border-border">
                  <th className="w-12 px-4 py-2.5 font-medium">Offer</th>
                  <th className="px-3 py-2.5 font-medium">Reference</th>
                  <th className="px-3 py-2.5 font-medium">Child</th>
                  <th className="px-3 py-2.5 font-medium">Class</th>
                  <th className="px-3 py-2.5 text-right font-medium">Score</th>
                  <th className="px-4 py-2.5 font-medium">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {list.map((c) => (
                  <tr key={c.applicationId}>
                    <td className="px-4 py-2.5">
                      {decidable(c) ? (
                        <input
                          type="checkbox"
                          className="size-4 accent-[var(--brand)]"
                          checked={c.decision === "Offer"}
                          disabled={busy !== null}
                          onChange={(e) => decide([c.applicationId], e.target.checked ? "Offer" : null, c.applicationId)}
                          aria-label={`Offer a place to ${c.childName}`}
                        />
                      ) : null}
                    </td>
                    <td className="tabular px-3 py-2.5">{c.referenceNumber}</td>
                    <td className="px-3 py-2.5 font-medium">{c.childName}</td>
                    <td className="px-3 py-2.5">{c.classLevel}</td>
                    <td className="tabular px-3 py-2.5 text-right">{c.score ?? "—"}</td>
                    <td className="px-4 py-2.5">
                      <span className="flex flex-wrap gap-1">
                        <Badge>{applicationStatusLabel[c.status]}</Badge>
                        {c.stageOneNotifiedAt && <Badge tone="success">Emailed</Badge>}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <ConfirmDialog
        open={confirmNotify}
        onOpenChange={setConfirmNotify}
        title={`Email ${toNotify} ${toNotify === 1 ? "family" : "families"} that they passed the first stage?`}
        confirmLabel="Send emails"
        pendingLabel="Queuing…"
        onConfirm={async () => {
          const r = await runAction(() => notifyPassed(sessionId), { success: (x) => `${x.queued} ${x.queued === 1 ? "email" : "emails"} queued`, onProblem: showError("Couldn't send the emails") });
          if (r) startTransition(() => router.refresh());
          return r !== null;
        }}
      >
        <p>Each family is emailed once; families already emailed are skipped.</p>
      </ConfirmDialog>

      <ConfirmDialog
        open={confirmPublish}
        onOpenChange={setConfirmPublish}
        title={`Offer ${offers}, not offer ${others}?`}
        confirmLabel="Publish decisions"
        pendingLabel="Publishing…"
        onConfirm={async () => {
          const r = await runAction(() => publishDecisions(sessionId), {
            success: (x) => `Published: ${x.offered} offered, ${x.notOffered} not offered`,
            onProblem: showError("Couldn't publish decisions"),
          });
          if (r) startTransition(() => router.refresh());
          return r !== null;
        }}
      >
        <p>
          Ticked applicants get an offer email with the admission letter; every other scored applicant gets a polite regret email. Applicants still awaiting a score aren&apos;t affected, so you can publish again later.
        </p>
      </ConfirmDialog>
    </div>
  );
}
