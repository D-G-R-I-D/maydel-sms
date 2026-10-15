import type { AttendanceStatus, ClassRegister } from "@/lib/api/types";

/** Daily register rules (E9.1–E9.2). The API is the authority; these shape the screen. */

export const STATUSES: { value: AttendanceStatus; short: string; label: string }[] = [
  { value: "Present", short: "P", label: "Present" },
  { value: "Absent", short: "A", label: "Absent" },
  { value: "Late", short: "L", label: "Late" },
  { value: "Excused", short: "E", label: "Excused" },
];

export const NOTE_MAX = 200;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export function isIsoDate(v: string | null | undefined): v is string {
  if (!v || !ISO_DATE.test(v)) return false;
  const d = new Date(`${v}T12:00:00Z`);
  return !Number.isNaN(d.getTime()) && d.toISOString().slice(0, 10) === v;
}

/** Shift a YYYY-MM-DD date by whole days, skipping Saturdays and Sundays (no register then). */
export function shiftSchoolDay(date: string, by: -1 | 1): string {
  const d = new Date(`${date}T12:00:00Z`);
  do d.setUTCDate(d.getUTCDate() + by);
  while (d.getUTCDay() === 0 || d.getUTCDay() === 6);
  return d.toISOString().slice(0, 10);
}

const dayFormat = new Intl.DateTimeFormat("en-NG", { weekday: "long", day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });
export function formatRegisterDate(date: string): string {
  return dayFormat.format(new Date(`${date}T12:00:00Z`));
}

/** Below 90 % amber, below 75 % red. */
export function attendanceTone(percentage: number | null): "danger" | "warning" | null {
  if (percentage === null) return null;
  if (percentage < 75) return "danger";
  if (percentage < 90) return "warning";
  return null;
}

export type RegisterMarks = Record<string, { status: AttendanceStatus; note: string }>;

export function marksFrom(register: Pick<ClassRegister, "entries">): RegisterMarks {
  return Object.fromEntries(register.entries.map((e) => [e.studentId, { status: e.status, note: e.note ?? "" }]));
}

/** Notes only matter when the student isn't present. */
const effective = (m: { status: AttendanceStatus; note: string }) => ({ status: m.status, note: m.status === "Present" ? "" : m.note.trim() });

export function marksDirty(marks: RegisterMarks, saved: RegisterMarks): boolean {
  return Object.keys(saved).some((id) => {
    const a = effective(marks[id] ?? saved[id]);
    const b = effective(saved[id]);
    return a.status !== b.status || a.note !== b.note;
  });
}

export function countMarks(marks: RegisterMarks): Record<AttendanceStatus, number> {
  const c: Record<AttendanceStatus, number> = { Present: 0, Absent: 0, Late: 0, Excused: 0 };
  for (const m of Object.values(marks)) c[m.status]++;
  return c;
}

/** The PUT body's entries: every unlocked student, notes only when not present. */
export function registerPayload(register: Pick<ClassRegister, "entries">, marks: RegisterMarks) {
  return register.entries
    .filter((e) => !e.locked)
    .map((e) => {
      const m = effective(marks[e.studentId] ?? { status: e.status, note: e.note ?? "" });
      return { studentId: e.studentId, status: m.status, note: m.note || null };
    });
}

export function noteErrors(marks: RegisterMarks): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const [id, m] of Object.entries(marks)) if (m.status !== "Present" && m.note.trim().length > NOTE_MAX) errors[id] = `Use ${NOTE_MAX} characters or fewer.`;
  return errors;
}
