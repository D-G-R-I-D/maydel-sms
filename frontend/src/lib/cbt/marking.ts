import type { components } from "@/lib/api/schema";

/** E16.4–E16.7: marking, releasing results, the grade sheet push and analytics (staff). */

type Schemas = components["schemas"];
export type AttemptRows = Schemas["AttemptRows"];
export type AttemptRow = Schemas["AttemptRow"];
export type AttemptReview = Schemas["AttemptReview"];
export type ReviewItem = Schemas["ReviewItem"];
export type QuestionResponses = Schemas["QuestionResponses"];
export type QuestionResponse = Schemas["QuestionResponse"];
export type GradebookPreview = Schemas["GradebookPreview"];
export type GradebookRow = Schemas["GradebookRow"];
export type PushState = Schemas["PushState"];
export type TestAnalytics = Schemas["TestAnalytics"];
export type ItemAnalysis = Schemas["ItemAnalysis"];
export type ChildTestResult = Schemas["ChildTestResult"];

/** Kinds a teacher may need to mark (the computer marks the rest, and a teacher can still override). */
export const TEACHER_MARKED = new Set(["ShortAnswer", "Theory", "FillInTheGap"]);

/** A typed mark: 0 to the question's marks, up to 2 decimals. Null when fine. */
export function markError(raw: string, marks: number): string | null {
  const v = raw.trim();
  if (v === "") return "Enter a mark.";
  if (!/^\d+(\.\d{1,2})?$/.test(v)) return "Use a number with up to 2 decimals.";
  if (Number(v) > marks) return `The most this question can get is ${marks}.`;
  return null;
}

export const percentOf = (score: number, total: number) => (total > 0 ? Math.round((score / total) * 1000) / 10 : 0);

export const rowStateLabel: Record<AttemptRow["state"], string> = { NotStarted: "Not started", InProgress: "Sitting", Submitted: "Handed in" };
export const rowStateTone: Record<AttemptRow["state"], "neutral" | "warning" | "success"> = { NotStarted: "neutral", InProgress: "warning", Submitted: "success" };

export const pushStateLabel: Record<PushState, string> = {
  Ready: "Ready",
  ReportPublished: "Report published: not changed",
  NotInClass: "No longer in the class",
  NotYourClass: "Not your class",
};
export const pushStateTone: Record<PushState, "success" | "warning" | "neutral" | "danger"> = { Ready: "success", ReportPublished: "warning", NotInClass: "neutral", NotYourClass: "neutral" };

/** Why Release is blocked, in plain words, from what the page knows; null = it can be released. */
export function releaseBlocker(o: { released: boolean; closesAt: string; now: number; sitting: number; needsMarking: number; submitted: number }): string | null {
  if (o.released) return "Results are released.";
  if (Date.parse(o.closesAt) > o.now) return "The test is still open. You can release results after it closes.";
  if (o.sitting > 0) return `${o.sitting} ${o.sitting === 1 ? "student is" : "students are"} still sitting.`;
  if (o.needsMarking > 0) return `${o.needsMarking} ${o.needsMarking === 1 ? "answer needs" : "answers need"} marking first.`;
  if (o.submitted === 0) return "Nobody has handed in yet.";
  return null;
}

/** Facility (share of marks earned, 0–1): red under 0.2, amber under 0.4. */
export const facilityTone = (f: number | null): "danger" | "warning" | "success" | "neutral" => (f === null ? "neutral" : f < 0.2 ? "danger" : f < 0.4 ? "warning" : "success");
