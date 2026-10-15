"use client";

import { useCallback, useEffect, useState } from "react";
import { Mail, RefreshCw, RotateCcw } from "lucide-react";
import type { Loaded } from "@/lib/api/server";
import type { ReportEmailStatus } from "@/lib/api/types";
import { loadReportEmails, retryReportEmail } from "@/lib/reports/actions";
import { EMAIL_POLL_MS, emailCounts, emailStatusLabel, emailStatusTone, hasPendingEmails } from "@/lib/reports/emails";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Card, cn } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";

const timeFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });
const when = (iso: string | null) => (iso ? timeFormat.format(new Date(iso)) : "");

/**
 * E12.3: report-card emails to parents for this class and term (admin). Refreshes every 15 s while
 * any are still sending; failed ones can be retried. `version` changes after a publish to reload.
 */
export function ReportEmails({ classRoomId, termId, initial, version }: { classRoomId: string; termId: string; initial: Loaded<ReportEmailStatus[]>; version: number }) {
  const [state, setState] = useState(initial);
  const [loading, setLoading] = useState(false);
  const [retrying, setRetrying] = useState<string | null>(null);

  // Quiet reload (polling, after publish/retry): no state change until the response is in.
  const refresh = useCallback(async () => {
    try {
      setState(await loadReportEmails(classRoomId, termId));
    } catch {
      // offline: keep what we have; the next tick or the Refresh button tries again
    }
  }, [classRoomId, termId]);

  const refreshNow = async () => {
    setLoading(true);
    await refresh();
    setLoading(false);
  };

  // After a publish: reload once the new emails are queued (state updates when the response arrives).
  useEffect(() => {
    if (version === 0) return;
    let live = true;
    loadReportEmails(classRoomId, termId)
      .then((r) => live && setState(r))
      .catch(() => {});
    return () => {
      live = false;
    };
  }, [version, classRoomId, termId]);

  const pending = state.ok && hasPendingEmails(state.data);
  useEffect(() => {
    if (!pending) return;
    const t = setInterval(() => void refresh(), EMAIL_POLL_MS);
    return () => clearInterval(t);
  }, [pending, refresh]);

  const retry = async (id: string) => {
    setRetrying(id);
    const ok = await runAction(() => retryReportEmail(id), { success: "Email queued again" });
    setRetrying(null);
    if (ok) await refresh();
  };

  const counts = state.ok ? emailCounts(state.data) : null;

  return (
    <Card className="overflow-hidden">
      <div className="flex flex-wrap items-center gap-3 border-b border-border px-5 py-4">
        <div className="min-w-0 flex-1">
          <h2 className="flex items-center gap-2 font-display text-lg font-semibold">
            <Mail className="size-5 text-brand" aria-hidden />
            Parent emails
          </h2>
          <p className="text-sm text-muted">
            {counts && state.ok && state.data.length > 0
              ? `${counts.Sent} sent · ${counts.Pending} sending · ${counts.Failed} failed${counts.Skipped ? ` · ${counts.Skipped} skipped` : ""}`
              : "Report cards emailed to guardians when results are published."}
          </p>
        </div>
        <Button variant="ghost" size="sm" onClick={() => void refreshNow()} loading={loading} loadingText="Refreshing…">
          <RefreshCw className="size-4" aria-hidden />
          Refresh
        </Button>
      </div>

      {!state.ok ? (
        <ProblemState problem={state.problem} />
      ) : state.data.length === 0 ? (
        <EmptyState icon={Mail} title="No emails for this term yet">
          Publish with &ldquo;Email each parent a copy&rdquo; ticked to send report cards to guardians with an email address.
        </EmptyState>
      ) : (
        <ul className="divide-y divide-border" aria-live="polite">
          {state.data.map((e) => (
            <li key={e.id} className={cn("flex flex-wrap items-start gap-x-4 gap-y-1 px-5 py-3", e.status === "Skipped" && "opacity-70")}>
              <div className="min-w-0 flex-1 basis-56">
                <p className="truncate font-medium">{e.student}</p>
                <p className="truncate text-sm text-muted">
                  {e.guardian} · <span className="break-all">{e.toAddress}</span>
                </p>
                {e.lastError && e.status !== "Sent" && (
                  <p className="mt-1 line-clamp-2 text-xs text-danger" title={e.lastError}>
                    {e.lastError}
                  </p>
                )}
              </div>
              <div className="flex shrink-0 flex-wrap items-center gap-3">
                <span className="tabular text-xs text-muted">
                  {e.status === "Sent" ? `Sent ${when(e.sentAt)}` : `Queued ${when(e.queuedAt)}`}
                  {e.attempts > 1 && ` · ${e.attempts} attempts`}
                </span>
                <Badge tone={emailStatusTone[e.status]}>{emailStatusLabel[e.status]}</Badge>
                {e.status === "Failed" && (
                  <Button size="sm" variant="secondary" onClick={() => void retry(e.id)} loading={retrying === e.id} loadingText="Retrying…" aria-label={`Retry email to ${e.guardian} about ${e.student}`}>
                    <RotateCcw className="size-3.5" aria-hidden />
                    Retry
                  </Button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
