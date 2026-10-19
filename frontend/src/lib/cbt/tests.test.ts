import { describe, expect, it } from "vitest";
import { newHeaderValues, newSection, scalingLine, sectionErrors, testHeaderSchema, toSectionsInput, toTestInput, totals, type QuestionBrief, type TestHeaderValues } from "./tests";

const header = (over: Partial<TestHeaderValues> = {}): TestHeaderValues => ({
  ...newHeaderValues("term-1", new Date("2026-10-18T10:00:00Z")),
  title: "Week 6 test",
  scope: "subj|level",
  classRoomIds: ["arm-a"],
  ...over,
});
const errorsOf = (v: TestHeaderValues) => {
  const r = testHeaderSchema.safeParse(v);
  return r.success ? {} : Object.fromEntries(r.error.issues.map((i) => [String(i.path[0]), i.message]));
};
const brief = (id: string): QuestionBrief => ({ id, kind: "Objective", text: id, topic: null, difficulty: "Medium", hasImage: false, isArchived: false });

describe("test header", () => {
  it("defaults to tomorrow 09:00–17:00 Lagos and sends offsets", () => {
    const v = header();
    expect([v.opensDate, v.opensTime, v.closesTime]).toEqual(["2026-10-19", "09:00", "17:00"]);
    expect(errorsOf(v)).toEqual({});
    const input = toTestInput({ ...v, componentId: "", instructions: "  " });
    expect(input).toMatchObject({ subjectId: "subj", classLevelId: "level", componentId: null, instructions: null, opensAt: "2026-10-19T09:00:00+01:00", closesAt: "2026-10-19T17:00:00+01:00", durationMinutes: 40, maxAttempts: 1 });
  });

  it("needs a class, a window after opening and long enough for the sitting", () => {
    expect(errorsOf(header({ classRoomIds: [] })).classRoomIds).toMatch(/at least one class/);
    expect(errorsOf(header({ closesTime: "08:00" })).closesTime).toBe("The test must close after it opens.");
    expect(errorsOf(header({ closesTime: "09:30" })).closesTime).toMatch(/shorter than the sitting/);
    expect(errorsOf(header({ durationMinutes: "4" })).durationMinutes).toMatch(/between 5 and 300/);
    expect(errorsOf(header({ maxAttempts: "6" })).maxAttempts).toMatch(/between 1 and 5/);
  });
});

describe("sections", () => {
  it("totals fixed and random sections, and sends only what each mode uses", () => {
    const a = { ...newSection("Objective", 0), questions: [brief("q1"), brief("q2")], marksPerQuestion: "2", topic: "stale" };
    const b = { ...newSection("Theory", 1), mode: "Random" as const, randomCount: "3", marksPerQuestion: "10.5", topic: " Plants ", difficulty: "Hard" as const };
    expect(totals([a, b])).toEqual({ marks: [4, 31.5], questionCount: 5, totalMarks: 35.5 });
    const [sa, sb] = toSectionsInput([a, b]);
    expect(sa).toMatchObject({ title: "Section A", mode: "Fixed", questionIds: ["q1", "q2"], randomCount: null, topic: null, difficulty: null });
    expect(sb).toMatchObject({ title: "Section B", mode: "Random", questionIds: null, randomCount: 3, topic: "Plants", difficulty: "Hard", marksPerQuestion: 10.5 });
  });

  it("flags bad titles, marks and draw counts", () => {
    const s = newSection();
    expect(sectionErrors([{ ...s, title: " " }])[s.key]).toMatch(/title/);
    expect(sectionErrors([{ ...s, marksPerQuestion: "0" }])[s.key]).toMatch(/more than 0/);
    expect(sectionErrors([{ ...s, marksPerQuestion: "1.255" }])[s.key]).toMatch(/2 decimals/);
    expect(sectionErrors([{ ...s, mode: "Random", randomCount: "0" }])[s.key]).toMatch(/between 1 and 200/);
    expect(sectionErrors([s])).toEqual({});
  });

  it("describes the scaling", () => {
    expect(scalingLine(100, { id: "c", name: "Exams", maxScore: 40 })).toBe("100 marks → Exams /40 (×0.40)");
    expect(scalingLine(0, { id: "c", name: "CAT", maxScore: 30 })).toMatch(/once it has marks/);
    expect(scalingLine(20, null)).toMatch(/Practice/);
  });
});
