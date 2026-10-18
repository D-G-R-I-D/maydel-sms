import type { components } from "@/lib/api/schema";

/** E11.6–E11.8: entrance exam sittings, scores, results and offers. Times are UTC from the API, shown in Lagos time. */

export type ExamSlot = components["schemas"]["ExamSlotDto"];
export type ExamSlotOption = components["schemas"]["ExamSlotOption"];
export type ExamCandidate = components["schemas"]["ExamCandidate"];
export type ExamResults = components["schemas"]["ExamResults"];
export type ExamMode = components["schemas"]["ExamMode"];
export type OfferDecision = components["schemas"]["OfferDecision"];

const LAGOS = "Africa/Lagos";
const dateTime = new Intl.DateTimeFormat("en-NG", { weekday: "short", day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: LAGOS });
const timeOnly = new Intl.DateTimeFormat("en-NG", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: LAGOS });

/** "Sat, 24 Oct 2026, 09:00" (Lagos). */
export function formatSlotStart(iso: string): string {
  return dateTime.format(new Date(iso));
}

/** "Sat, 24 Oct 2026, 09:00–11:00" (Lagos). */
export function formatSlotRange(startIso: string, endIso: string): string {
  return `${dateTime.format(new Date(startIso))}–${timeOnly.format(new Date(endIso))}`;
}

export const modeLabel: Record<ExamMode, string> = { Paper: "Paper", Cbt: "Computer-based (CBT)" };

/** Lagos is UTC+1 all year (no DST), so a local date + time → ISO with +01:00. */
export function lagosIso(date: string, time: string): string {
  return `${date}T${time}:00+01:00`;
}

/** The Lagos date ("2026-10-24") and time ("09:00") of a UTC instant, for prefilling the sitting form. */
export function toLagosParts(iso: string): { date: string; time: string } {
  const shifted = new Date(new Date(iso).getTime() + 60 * 60 * 1000);
  const s = shifted.toISOString();
  return { date: s.slice(0, 10), time: s.slice(11, 16) };
}

export type SlotInput = { date: string; time: string; durationMinutes: string; venue: string; mode: ExamMode; capacity: string; notes: string };
export type SlotErrors = Partial<Record<keyof SlotInput, string>>;

export function slotErrors(v: SlotInput, now: Date = new Date()): SlotErrors {
  const e: SlotErrors = {};
  if (!/^\d{4}-\d{2}-\d{2}$/.test(v.date)) e.date = "Choose the date.";
  if (!/^\d{2}:\d{2}$/.test(v.time)) e.time = "Choose the start time.";
  else if (!e.date && new Date(lagosIso(v.date, v.time)).getTime() <= now.getTime()) e.time = "The sitting must start in the future.";
  const d = Number(v.durationMinutes);
  if (!Number.isInteger(d) || d < 15 || d > 480) e.durationMinutes = "Enter 15 to 480 minutes.";
  if (!v.venue.trim()) e.venue = "Enter the venue.";
  else if (v.venue.trim().length > 120) e.venue = "Use 120 characters or fewer.";
  const c = Number(v.capacity);
  if (!Number.isInteger(c) || c < 1 || c > 1000) e.capacity = "Enter 1 to 1,000 seats.";
  if (v.notes.length > 500) e.notes = "Use 500 characters or fewer.";
  return e;
}

/** Exam score 0–100 (a percentage), at most 2 decimal places, like the API. */
export function scoreError(input: string): string | null {
  if (input.trim() === "") return "Enter a score.";
  const n = Number(input);
  if (!Number.isFinite(n) || n < 0 || n > 100) return "Scores run from 0 to 100.";
  if (!/^\d+(\.\d{1,2})?$/.test(input.trim())) return "Use at most 2 decimal places.";
  return null;
}

export function cutOffError(input: string): string | null {
  if (input.trim() === "") return null; // clearing the cut-off is allowed
  return scoreError(input);
}
