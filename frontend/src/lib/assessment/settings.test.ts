import { describe, expect, it } from "vitest";
import { activeTotal, move, settingsIssues, toPayload, toRows } from "./settings";

const settings = {
  components: [
    { id: "b", name: "Exams", maxScore: 40, order: 2, isActive: true, hasScores: true },
    { id: "a", name: "CAT", maxScore: 30, order: 1, isActive: true, hasScores: false },
    { id: "c", name: "Project", maxScore: 30, order: 3, isActive: true, hasScores: false },
    { id: "d", name: "Old", maxScore: 10, order: 4, isActive: false, hasScores: true },
  ],
  bands: [
    { grade: "F", minScore: 0, maxScore: 39.99, remark: "Needs improvement" },
    { grade: "A", minScore: 70, maxScore: 100, remark: "Excellent" },
    { grade: "C", minScore: 40, maxScore: 69.99, remark: "Good" },
  ],
};

describe("assessment settings", () => {
  it("loads components in report order and bands highest first", () => {
    const { components, bands } = toRows(settings);
    expect(components.map((c) => c.name)).toEqual(["CAT", "Exams", "Project", "Old"]);
    expect(bands.map((b) => b.grade)).toEqual(["A", "C", "F"]);
  });

  it("totals active components only", () => {
    expect(activeTotal(toRows(settings).components)).toBe(100);
  });

  it("is valid as loaded", () => {
    const { components, bands } = toRows(settings);
    expect(settingsIssues(components, bands)).toEqual([]);
  });

  it("explains what blocks saving", () => {
    const { components, bands } = toRows(settings);
    components[0] = { ...components[0], maxScore: "25" };
    bands[2] = { ...bands[2], minScore: "5" };
    bands[1] = { ...bands[1], grade: "A" };
    expect(settingsIssues(components, bands)).toEqual(
      expect.arrayContaining(["Active components add up to 95; they must total 100.", "The lowest band must start at 0.", "Grade A appears twice."]),
    );
  });

  it("builds the PUT body and reorders rows", () => {
    const { components, bands } = toRows(settings);
    const body = toPayload(move(components, 3, 0), bands);
    expect(body.components[0]).toEqual({ id: "d", name: "Old", maxScore: 10, isActive: false });
    expect(body.bands.map((b) => b.minScore)).toEqual([70, 40, 0]);
    expect(move([1, 2, 3], 0, 5)).toEqual([1, 2, 3]);
  });
});
