import type { components } from "@/lib/api/schema";

/** E3.6 end-of-session promotion: preview per class, exceptions, apply in one go, undo per class. */
export type PromotionPlan = components["schemas"]["PromotionPlan"];
export type PromotionClass = components["schemas"]["PromotionClass"];
export type PromotionStudent = components["schemas"]["PromotionStudent"];
export type PromotionOutcome = components["schemas"]["PromotionOutcome"];
export type PromotionDecision = components["schemas"]["PromotionDecision"];
export type PromotionSummary = components["schemas"]["PromotionSummary"];
export type PromotionUndone = components["schemas"]["PromotionUndone"];

export const PromotionCodes = { alreadyDone: "promotion.already_done", classFull: "promotion.class_full", tooEarly: "promotion.too_early" } as const;

export const outcomes: { value: PromotionOutcome; label: string }[] = [
  { value: "Promote", label: "Promote" },
  { value: "Repeat", label: "Repeat" },
  { value: "Graduate", label: "Graduate" },
  { value: "Withdraw", label: "Withdraw" },
];
export const doneLabel: Record<PromotionOutcome, string> = { Promote: "Promoted", Repeat: "Repeating", Graduate: "Graduated", Withdraw: "Withdrawn" };

export type Choice = { outcome: PromotionOutcome; toClassRoomId: string | null };

/** The server's suggestion for each student still to be moved on (done rows are left out). */
export function initialChoices(plan: PromotionPlan): Record<string, Choice> {
  const out: Record<string, Choice> = {};
  for (const c of plan.classes)
    for (const s of c.students) if (!s.done) out[s.studentId] = { outcome: s.suggestedOutcome, toClassRoomId: s.suggestedClassRoomId ?? c.nextClassRoomId };
  return out;
}

/** Why a choice can't be sent yet, or null (mirrors promotion.choose_class / same_class). */
export function choiceError(choice: Choice, fromClassRoomId: string): string | null {
  if (choice.outcome !== "Promote") return null;
  if (!choice.toClassRoomId) return "Choose a class.";
  if (choice.toClassRoomId === fromClassRoomId) return "Same class: use Repeat.";
  return null;
}

/** Decisions for the given classes' pending students, as the API takes them. */
export function decisionsFor(classes: readonly PromotionClass[], choices: Record<string, Choice>): PromotionDecision[] {
  return classes.flatMap((c) =>
    c.students
      .filter((s) => !s.done && choices[s.studentId])
      .map((s) => {
        const ch = choices[s.studentId];
        return { studentId: s.studentId, outcome: ch.outcome, toClassRoomId: ch.outcome === "Promote" ? ch.toClassRoomId : null };
      }),
  );
}

/** "32 promoted, 2 repeating, 1 withdrawn, 40 graduating" (zero counts left out). */
export function summaryText(decisions: readonly PromotionDecision[]): string {
  const n = (o: PromotionOutcome) => decisions.filter((d) => d.outcome === o).length;
  const parts = [
    [n("Promote"), "promoted"],
    [n("Repeat"), "repeating"],
    [n("Withdraw"), "withdrawn"],
    [n("Graduate"), "graduating"],
  ] as const;
  return parts.filter(([c]) => c > 0).map(([c, w]) => `${c} ${w}`).join(", ") || "Nobody";
}

/** The result toast: "Done: 32 promoted, 40 graduated." */
export function resultText(s: PromotionSummary): string {
  const parts = [
    [s.promoted, "promoted"],
    [s.repeated, "repeating"],
    [s.withdrawn, "withdrawn"],
    [s.graduated, "graduated"],
  ] as const;
  return `Done: ${parts.filter(([c]) => c > 0).map(([c, w]) => `${c} ${w}`).join(", ") || "no changes"}.`;
}
