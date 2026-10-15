import type { EmailStatus, PublishResult, ReportEmailStatus } from "@/lib/api/types";

/** E12.3 parent emails: labels, badge tones and the "still sending" check used for polling. */

export const EMAIL_POLL_MS = 15_000;

/** Badge tones; Skipped is also dimmed where it is shown. */
export const emailStatusTone: Record<EmailStatus, "neutral" | "success" | "danger"> = {
  Pending: "neutral",
  Sent: "success",
  Failed: "danger",
  Skipped: "neutral",
};

export const emailStatusLabel: Record<EmailStatus, string> = {
  Pending: "Sending",
  Sent: "Sent",
  Failed: "Failed",
  Skipped: "Skipped",
};

export function hasPendingEmails(rows: readonly Pick<ReportEmailStatus, "status">[]): boolean {
  return rows.some((r) => r.status === "Pending");
}

export function emailCounts(rows: readonly Pick<ReportEmailStatus, "status">[]): Record<EmailStatus, number> {
  const counts: Record<EmailStatus, number> = { Pending: 0, Sent: 0, Failed: 0, Skipped: 0 };
  for (const r of rows) counts[r.status]++;
  return counts;
}

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

/** Toast after publishing, e.g. "Published 5 reports · 7 emails queued". */
export function publishToast(r: Pick<PublishResult, "published" | "emailsQueued">, notified: boolean): string {
  const published = `Published ${plural(r.published, "report", "reports")}`;
  return notified ? `${published} · ${plural(r.emailsQueued, "email", "emails")} queued` : published;
}

/** True when parents were to be emailed but nobody had an address on file. */
export function noGuardianEmails(r: Pick<PublishResult, "published" | "emailsQueued">, notified: boolean): boolean {
  return notified && r.published > 0 && r.emailsQueued === 0;
}
