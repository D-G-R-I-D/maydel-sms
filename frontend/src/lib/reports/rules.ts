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
export function publishPreview(summary: ClassReportSummary) {
  const scored = summary.rows.filter((r) => r.subjectsScored > 0);
  return {
    willPublish: scored.length,
    noScores: summary.rows.filter((r) => r.subjectsScored === 0),
    missingAttendance: summary.daysOpened === null ? scored : scored.filter((r) => r.daysPresent === null),
  };
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
