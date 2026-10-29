import type { components } from "@/lib/api/schema";

/** E18 school bus: buses, runs (pickup / drop-off batches), the trip log and the daily checklist. */
export type DriverToday = components["schemas"]["DriverToday"];
export type BusDay = components["schemas"]["BusDay"];
export type Bus = components["schemas"]["BusDto"];
export type Run = components["schemas"]["RunDto"];
export type Trip = components["schemas"]["TripDto"];
export type CheckQuestion = components["schemas"]["CheckQuestionDto"];
export type Check = components["schemas"]["CheckDto"];
export type CheckAnswerInput = components["schemas"]["CheckAnswerInput"];
export type TripEvent = components["schemas"]["TripEvent"];

export const TripCodes = { alreadyLeft: "trip.already_left", notLeft: "trip.not_left", alreadyBack: "trip.already_back", undoWindow: "trip.undo_window", nothing: "trip.nothing", notYours: "bus.not_yours" } as const;

/** Where a run stands: not left yet, out on the road, or back. */
export type TripState = "waiting" | "out" | "back";
export function tripState(t: Pick<Trip, "leftAt" | "backAt">): TripState {
  return t.backAt ? "back" : t.leftAt ? "out" : "waiting";
}

/** The next tap for a run, or null when it's done for the day. */
export function nextEvent(t: Pick<Trip, "leftAt" | "backAt">): TripEvent | null {
  const s = tripState(t);
  return s === "waiting" ? "Left" : s === "out" ? "Back" : null;
}

/** "1 h 05 min" / "42 min", or an em dash. */
export function duration(minutes: number | null | undefined): string {
  if (minutes == null) return "—";
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  return h ? `${h} h ${String(m).padStart(2, "0")} min` : `${m} min`;
}

/** Draft answers for a bus's checklist: one entry per active question, prefilled from today's check. */
export type AnswerDraft = { yes: boolean | null; number: string; text: string };
export function draftAnswers(questions: readonly CheckQuestion[], check: Check | null): Record<string, AnswerDraft> {
  const out: Record<string, AnswerDraft> = {};
  for (const q of questions) {
    const a = check?.answers.find((x) => x.questionId === q.id);
    out[q.id] = { yes: a?.yes ?? null, number: a?.number != null ? String(a.number) : "", text: a?.text ?? "" };
  }
  return out;
}

/** Every yes/no and number question must be answered (the server's rule); text is optional. */
export function missingAnswers(questions: readonly CheckQuestion[], drafts: Record<string, AnswerDraft>): string[] {
  return questions
    .filter((q) => q.isActive)
    .filter((q) => (q.kind === "YesNo" ? drafts[q.id]?.yes == null : q.kind === "Number" ? !/^\d+(\.\d+)?$/.test(drafts[q.id]?.number.trim() ?? "") : false))
    .map((q) => q.id);
}

/** True when this answer would flag the day (a "wrong" yes/no, or anything written where that flags). */
export function flagsDay(q: CheckQuestion, d: AnswerDraft | undefined): boolean {
  if (!d) return false;
  if (q.kind === "YesNo") return d.yes != null && d.yes !== q.expectedYes;
  return q.flagIfAnswered && (q.kind === "Text" ? d.text.trim() !== "" : d.number.trim() !== "");
}

export function toAnswerInputs(questions: readonly CheckQuestion[], drafts: Record<string, AnswerDraft>): CheckAnswerInput[] {
  return questions
    .filter((q) => q.isActive)
    .map((q) => {
      const d = drafts[q.id];
      return {
        questionId: q.id,
        yes: q.kind === "YesNo" ? (d?.yes ?? null) : null,
        number: q.kind === "Number" && d?.number.trim() ? Number(d.number) : null,
        text: q.kind === "Text" && d?.text.trim() ? d.text.trim() : null,
      };
    });
}
