import type { ClassReportRow, ClassReportSummary } from "@/lib/api/types";

/** Report helpers mirrored from the API rules (docs/handoffs/m1.2-reports.md). */

export const COMMENT_MAX = 2000;

/** "1st", "2nd", "3rd", "4th", "11th", "22nd" … */
export function ordinal(n: number): string {
  const mod100 = n % 100;
  if (mod100 >= 11 && mod100 <= 13) return `${n}th`;
  const suffixes: Record<number, string> = { 1: "st", 2: "nd", 3: "rd" };
  const suffix = suffixes[n % 10] ?? "th";
  return `${n}${suffix}`;
}

export function formatAverage(n: number | null | undefined): string {
  return n === null || n === undefined ? "—" : (Math.round(n * 100) / 100).toFixed(2);
}

/** What publishing would do now: who goes out, who is skipped (no scores), who blocks it (no attendance). */
/**
 * What Publish would do. Publishing only touches reports that aren't published yet: drafts with
 * scores are published, drafts without scores are skipped, published ones stay as they are.
 */
export function publishPreview(summary: ClassReportSummary) {
  const drafts = summary.rows.filter((r) => r.status !== "Published");
  const toPublish = drafts.filter((r) => r.subjectsScored > 0);
  const alreadyPublished = summary.rows.length - drafts.length;
  return {
    willPublish: toPublish.length,
    alreadyPublished,
    /** Every report is published: show a status instead of the Publish button. */
    allPublished: drafts.length === 0 && alreadyPublished > 0,
    noScores: drafts.filter((r) => r.subjectsScored === 0),
    missingAttendance: summary.daysOpened === null ? toPublish : toPublish.filter((r) => r.daysPresent === null),
  };
}

const publishedFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", timeZone: "Africa/Lagos" });

/** "14 Oct 2026" (Lagos), for "All 5 reports published · 14 Oct 2026". */
export function formatPublishedOn(iso: string | null): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? null : publishedFormat.format(d);
}

export function attendanceText(row: Pick<ClassReportRow, "daysPresent">, daysOpened: number | null): string {
  if (daysOpened === null || row.daysPresent === null) return "—";
  return `${row.daysPresent}/${daysOpened}`;
}

/** Attendance form rules: days opened 1–200; each present 0..opened (whole days). Returns errors by key. */
export function attendanceErrors(daysOpened: string, present: Record<string, string>): Record<string, string> {
  const errors: Record<string, string> = {};
  const opened = /^\d+$/.test(daysOpened.trim()) ? Number(daysOpened) : NaN;
  if (Number.isNaN(opened) || opened < 1 || opened > 200) errors.daysOpened = "Enter a whole number from 1 to 200.";
  for (const [id, v] of Object.entries(present)) {
    const t = v.trim();
    if (t === "") errors[id] = "Required.";
    else if (!/^\d+$/.test(t)) errors[id] = "Whole days only.";
    else if (!Number.isNaN(opened) && Number(t) > opened) errors[id] = `At most ${opened}.`;
  }
  return errors;
}

/** Neighbours in the class list, for prev/next on the report preview. */
export function neighbours<T extends { studentId: string }>(rows: readonly T[], studentId: string): { prev: T | null; next: T | null; index: number } {
  const i = rows.findIndex((r) => r.studentId === studentId);
  return { prev: i > 0 ? rows[i - 1] : null, next: i >= 0 && i < rows.length - 1 ? rows[i + 1] : null, index: i };
}

/** "+5.5", "−3", "0" (true minus sign), for change cells. */
export function formatChange(n: number | null | undefined): string {
  if (n === null || n === undefined) return "—";
  const r = Math.round(n * 100) / 100;
  if (r === 0) return "0";
  return r > 0 ? `+${r}` : `−${Math.abs(r)}`;
}

/** Short column label for an earlier term: "1st T", "2nd T". */
export function termShort(number: "First" | "Second" | "Third"): string {
  return { First: "1st T", Second: "2nd T", Third: "3rd T" }[number];
}

export const trendTone = { Improved: "success", Steady: "neutral", Declined: "danger" } as const;

export function reportPdfHref(studentId: string, termId: string): string {
  return `/api/media/reports/${studentId}/${termId}/pdf`;
}

export function classPdfHref(classRoomId: string, termId: string): string {
  return `/api/media/reports/class/pdf?${new URLSearchParams({ classRoomId, termId }).toString()}`;
}

/** File name from a Content-Disposition header (RFC 6266: filename* wins over filename). */
export function filenameFromDisposition(header: string | null): string | null {
  if (!header) return null;
  const star = /filename\*\s*=\s*(?:UTF-8|utf-8)''([^;]+)/.exec(header);
  if (star) {
    try {
      return decodeURIComponent(star[1].trim().replace(/^"|"$/g, ""));
    } catch {
      // fall through to the plain filename
    }
  }
  const plain = /filename\s*=\s*"?([^";]+)"?/i.exec(header);
  return plain ? plain[1].trim() : null;
}
