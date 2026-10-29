import { describe, expect, it } from "vitest";
import { choiceError, decisionsFor, initialChoices, resultText, summaryText, type PromotionClass, type PromotionPlan } from "./promotion";

const student = (id: string, over: Partial<PromotionClass["students"][number]> = {}): PromotionClass["students"][number] => ({
  studentId: id, fullName: `S ${id}`, admissionNumber: id, status: "Active", sessionAverage: 60, needsReview: false,
  suggestedOutcome: "Promote", suggestedClassRoomId: "y4a", done: null, ...over,
});
const cls: PromotionClass = { classRoomId: "y3a", name: "Year 3 A", levelOrder: 3, isFinalLevel: false, nextClassRoomId: "y4a", nextClassName: "Year 4 A",
  students: [student("a"), student("b", { needsReview: true }), student("c", { done: { outcome: "Promote", toClassRoomId: "y4a", toClassName: "Year 4 A", at: "2027-07-20T10:00:00Z" } })] };
const plan: PromotionPlan = { sessionId: "s", session: "2026/2027", canPromote: true, closedReason: null, classes: [cls] };

describe("promotion", () => {
  it("pre-fills pending students only", () => {
    expect(Object.keys(initialChoices(plan))).toEqual(["a", "b"]);
  });
  it("needs a different class to promote", () => {
    expect(choiceError({ outcome: "Promote", toClassRoomId: null }, "y3a")).toBe("Choose a class.");
    expect(choiceError({ outcome: "Promote", toClassRoomId: "y3a" }, "y3a")).toMatch(/Repeat/);
    expect(choiceError({ outcome: "Repeat", toClassRoomId: null }, "y3a")).toBeNull();
  });
  it("sends the class only for Promote and summarises the batch", () => {
    const d = decisionsFor([cls], { a: { outcome: "Promote", toClassRoomId: "y4a" }, b: { outcome: "Repeat", toClassRoomId: "y4a" } });
    expect(d).toEqual([{ studentId: "a", outcome: "Promote", toClassRoomId: "y4a" }, { studentId: "b", outcome: "Repeat", toClassRoomId: null }]);
    expect(summaryText(d)).toBe("1 promoted, 1 repeating");
    expect(resultText({ promoted: 32, repeated: 0, graduated: 40, withdrawn: 1 })).toBe("Done: 32 promoted, 1 withdrawn, 40 graduated.");
  });
});
