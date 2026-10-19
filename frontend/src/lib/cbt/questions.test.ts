import { describe, expect, it } from "vitest";
import { addAnswer, emptyValues, firstLine, gapCount, questionSchema, splitGap, toInput, type QuestionFormValues } from "./questions";

const scope = { subjectId: "s", classLevelId: "l" };
const errorsOf = (v: QuestionFormValues) => {
  const r = questionSchema.safeParse(v);
  return r.success ? {} : Object.fromEntries(r.error.issues.map((i) => [String(i.path[0]), i.message]));
};
const opts = (...marks: boolean[]) => marks.map((isCorrect, i) => ({ id: null, text: `Option ${i + 1}`, isCorrect }));

describe("question rules", () => {
  it("objective needs exactly one correct option; multi-select at least one", () => {
    const base = { ...emptyValues(), text: "2 + 2 = ?" };
    expect(errorsOf({ ...base, options: opts(false, false) }).options).toBe("Mark exactly one option as correct.");
    expect(errorsOf({ ...base, options: opts(true, true) }).options).toBe("Mark exactly one option as correct.");
    expect(errorsOf({ ...base, options: opts(true, false) })).toEqual({});
    expect(errorsOf({ ...base, kind: "MultiSelect", options: opts(false, false) }).options).toBe("Mark at least one option as correct.");
    expect(errorsOf({ ...base, kind: "MultiSelect", options: opts(true, true, false) })).toEqual({});
  });

  it("rejects blank, duplicate and too few options", () => {
    const base = { ...emptyValues(), text: "Pick one" };
    expect(errorsOf({ ...base, options: opts(true) }).options).toMatch(/between 2 and 8/);
    expect(errorsOf({ ...base, options: [...opts(true), { id: null, text: " ", isCorrect: false }] }).options).toBe("Every option needs text.");
    expect(errorsOf({ ...base, options: [{ id: null, text: "Lagos", isCorrect: true }, { id: null, text: "lagos ", isCorrect: false }] }).options).toBe("Two options are the same.");
  });

  it("fill in the gap needs one gap and an answer; theory needs a guide; true/false an answer", () => {
    const base = { ...emptyValues(), kind: "FillInTheGap" as const };
    expect(errorsOf({ ...base, text: "No gap", acceptedAnswers: ["x"] }).text).toMatch(/three underscores/);
    expect(errorsOf({ ...base, text: "The capital is ___ and ___", acceptedAnswers: ["x"] }).text).toMatch(/three underscores/);
    expect(errorsOf({ ...base, text: "The capital is ___.", acceptedAnswers: [] }).acceptedAnswers).toMatch(/at least one/);
    expect(errorsOf({ ...base, text: "The capital is ___.", acceptedAnswers: ["Abuja"] })).toEqual({});
    expect(errorsOf({ ...emptyValues(), kind: "Theory", text: "Explain photosynthesis." }).markingGuide).toMatch(/marking guide/);
    expect(errorsOf({ ...emptyValues(), kind: "TrueFalse", text: "The sun is a star." }).trueFalseAnswer).toMatch(/true or false/);
    expect(errorsOf({ ...emptyValues(), kind: "ShortAnswer", text: "Name a planet." })).toEqual({});
  });

  it("ignores a partial option row left in a restored draft when the kind has no options", () => {
    const v = { ...emptyValues(), kind: "FillInTheGap" as const, text: "The capital is ___.", acceptedAnswers: ["Abuja"], options: [...emptyValues().options, { text: "" } as never] };
    expect(errorsOf(v)).toEqual({});
  });

  it("sends only what the kind uses, keeping option ids", () => {
    const v: QuestionFormValues = { ...emptyValues(), text: " Q ", topic: " ", options: [{ id: "a", text: " A ", isCorrect: true }, { id: null, text: "B", isCorrect: false }], acceptedAnswers: ["stale"], markingGuide: "stale" };
    expect(toInput(v, scope)).toEqual({ ...scope, kind: "Objective", text: "Q", options: [{ id: "a", text: "A", isCorrect: true }, { id: null, text: "B", isCorrect: false }], trueFalseAnswer: null, acceptedAnswers: null, markingGuide: null, topic: null, difficulty: "Medium" });
    const tf = toInput({ ...v, kind: "TrueFalse", trueFalseAnswer: "false" }, scope);
    expect([tf.options, tf.trueFalseAnswer, tf.acceptedAnswers]).toEqual([null, false, null]);
    const sa = toInput({ ...v, kind: "ShortAnswer", markingGuide: " Award 1 mark " }, scope);
    expect([sa.acceptedAnswers, sa.markingGuide]).toEqual([["stale"], "Award 1 mark"]);
  });

  it("gap helpers, answer chips and first line", () => {
    expect(gapCount("a ___ b _____")).toBe(2);
    expect(splitGap("The capital is ____.")).toEqual(["The capital is ", "."]);
    expect(splitGap("none")).toBeNull();
    expect(addAnswer(["Abuja"], " abuja ").error).toMatch(/already/);
    expect(addAnswer(["Abuja"], " FCT ").list).toEqual(["Abuja", "FCT"]);
    expect(addAnswer([], "   ").list).toEqual([]);
    expect(firstLine("Line one\nLine two")).toBe("Line one");
    expect(firstLine("x".repeat(200), 10)).toHaveLength(10);
  });
});
