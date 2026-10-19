import type { components } from "@/lib/api/schema";
import { formatLagos } from "@/lib/time/lagos";

/** E16.3: a student sitting a test. Time comes from the server; answers autosave one item at a time. */

type Schemas = components["schemas"];
export type StudentTest = Schemas["StudentTestDto"];
export type StudentTestState = Schemas["StudentTestState"];
export type AttemptPaper = Schemas["AttemptPaper"];
export type PaperItem = Schemas["PaperItem"];
export type PaperSection = Schemas["PaperSection"];

export const MAX_ANSWER_TEXT = 5000;
export const AUTOSAVE_MS = 800;

/** What the student has put for one item, in the API's shape (minus the session token). */
export type Answer = { optionIds: string[] | null; trueFalse: boolean | null; text: string | null };

export function answerOf(item: PaperItem): Answer {
  return { optionIds: item.answerOptionIds, trueFalse: item.answerTrueFalse, text: item.answerText };
}

/** The body for one item: only the field its kind uses, so a cleared answer is sent as empty. */
export function answerBody(kind: PaperItem["kind"], a: Answer): Answer {
  if (kind === "Objective" || kind === "MultiSelect") return { optionIds: a.optionIds ?? [], trueFalse: null, text: null };
  if (kind === "TrueFalse") return { optionIds: null, trueFalse: a.trueFalse, text: null };
  return { optionIds: null, trueFalse: null, text: (a.text ?? "").slice(0, MAX_ANSWER_TEXT) };
}

export function isAnswered(kind: PaperItem["kind"], a: Answer): boolean {
  if (kind === "Objective" || kind === "MultiSelect") return (a.optionIds ?? []).length > 0;
  if (kind === "TrueFalse") return a.trueFalse !== null;
  return (a.text ?? "").trim() !== "";
}

/** "45:00", or "1:05:09" over an hour. Never negative. */
export function formatRemaining(seconds: number): string {
  const s = Math.max(0, Math.floor(seconds));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const pad = (n: number) => String(n).padStart(2, "0");
  return h > 0 ? `${h}:${pad(m)}:${pad(s % 60)}` : `${pad(m)}:${pad(s % 60)}`;
}

/** Amber under 5 minutes, red under 1. */
export const timerTone = (seconds: number): "normal" | "warning" | "danger" => (seconds < 60 ? "danger" : seconds < 300 ? "warning" : "normal");

/** A polite announcement as the timer crosses 5 and 1 minutes left; null otherwise. */
export function timerAnnouncement(previous: number, current: number): string | null {
  if (previous > 300 && current <= 300) return "5 minutes left.";
  if (previous > 60 && current <= 60) return "1 minute left.";
  return null;
}

/** "2h 5m", "25m", "40s". */
export function formatSpan(ms: number): string {
  const s = Math.max(0, Math.round(ms / 1000));
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m`;
  const h = Math.floor(m / 60);
  if (h < 48) return m % 60 ? `${h}h ${m % 60}m` : `${h}h`;
  return `${Math.floor(h / 24)} days`;
}

/** The window line on a test card. */
export function windowLabel(t: Pick<StudentTest, "state" | "opensAt" | "closesAt">, now: number): string {
  const opens = Date.parse(t.opensAt);
  const closes = Date.parse(t.closesAt);
  if (t.state === "Upcoming") return opens - now < 24 * 3600 * 1000 ? `Opens in ${formatSpan(opens - now)}` : `Opens ${formatLagos(t.opensAt)}`;
  if (t.state === "Open" || t.state === "InProgress") return closes - now < 24 * 3600 * 1000 ? `Closes in ${formatSpan(closes - now)}` : `Closes ${formatLagos(t.closesAt)}`;
  return `Closed ${formatLagos(t.closesAt)}`;
}

export const stateLabel: Record<StudentTestState, string> = { Upcoming: "Upcoming", Open: "Open", InProgress: "In progress", Done: "Handed in", Missed: "Missed" };
export const stateTone: Record<StudentTestState, "neutral" | "brand" | "success" | "warning" | "danger"> = { Upcoming: "neutral", Open: "brand", InProgress: "warning", Done: "success", Missed: "danger" };

/** Question numbers (1-based order) still unanswered, for the submit confirm. */
export function unanswered(items: PaperItem[], answers: Record<string, Answer>): number[] {
  return [...items].sort((a, b) => a.order - b.order).filter((i) => !isAnswered(i.kind, answers[i.id] ?? answerOf(i))).map((i) => i.order + 1);
}

/** "3, 7 and 12"; long lists are cut ("3, 7, 12 and 9 more"). */
export function listNumbers(nums: number[], max = 10): string {
  if (nums.length === 0) return "";
  if (nums.length > max) return `${nums.slice(0, max).join(", ")} and ${nums.length - max} more`;
  return nums.length === 1 ? String(nums[0]) : `${nums.slice(0, -1).join(", ")} and ${nums.at(-1)}`;
}

export const attemptImageUrl = (attemptId: string, itemId: string) => `/api/media/cbt/attempts/${attemptId}/items/${itemId}/image`;
