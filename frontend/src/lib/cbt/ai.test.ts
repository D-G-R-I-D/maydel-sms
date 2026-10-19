import { describe, expect, it } from "vitest";
import { aiRequestErrors, balancedMix, draftProblem, draftValues, quotaBlocker, toDraftInput, type AiQuota } from "./ai";

const quota: AiQuota = { available: true, enabled: true, allowance: 30, used: 18, remaining: 12, schoolCap: 500, schoolUsed: 100, resetsAt: "2026-11-01T00:00:00+01:00" };
const base = { count: 10, easy: 3, medium: 4, hard: 3, kinds: ["Objective"], guidance: "Fractions", files: [] };

describe("AI question writing", () => {
  it("explains why generating is blocked", () => {
    expect(quotaBlocker(quota)).toBeNull();
    expect(quotaBlocker({ ...quota, available: false })).toMatch(/isn't set up yet/);
    expect(quotaBlocker({ ...quota, enabled: false })).toMatch(/switched off/);
    expect(quotaBlocker({ ...quota, remaining: 0 })).toMatch(/used this month's allowance \(30\)/);
    expect(quotaBlocker({ ...quota, schoolUsed: 500 })).toMatch(/school has used/);
  });

  it("checks the request like the API", () => {
    expect(aiRequestErrors(base)).toEqual({});
    expect(aiRequestErrors({ ...base, kinds: [] }).kinds).toMatch(/at least one kind/);
    expect(aiRequestErrors({ ...base, count: 41 }).count).toMatch(/between 1 and 40/);
    expect(aiRequestErrors({ ...base, hard: 4 }).easy).toMatch(/add up to 10/);
    expect(aiRequestErrors({ ...base, guidance: "" }).guidance).toMatch(/Upload your notes/);
    expect(aiRequestErrors({ ...base, guidance: "", files: [{ name: "n.pdf", type: "application/pdf", size: 6 * 1024 * 1024 }] }).files).toMatch(/5 MB/);
    expect(aiRequestErrors({ ...base, files: [{ name: "n.heic", type: "image/heic", size: 10 }] }).files).toMatch(/HEIC/);
    expect(aiRequestErrors({ ...base, files: Array(6).fill({ name: "a.png", type: "image/png", size: 10 }) }).files).toMatch(/at most 5/);
  });

  it("splits the difficulty mix, leaning to medium", () => {
    expect(balancedMix(10)).toEqual({ easy: 3, medium: 4, hard: 3 });
    expect(balancedMix(1)).toEqual({ easy: 0, medium: 1, hard: 0 });
  });

  it("turns a generated question into editable values and back", () => {
    const v = draftValues({ kind: "Objective", text: "2 + 2?", options: [{ text: "4", isCorrect: true }, { text: "5", isCorrect: false }], trueFalseAnswer: null, acceptedAnswers: [], markingGuide: null, topic: "Sums", difficulty: "Easy" });
    expect(draftProblem(v)).toBeNull();
    expect(toDraftInput(v)).toEqual({ kind: "Objective", text: "2 + 2?", options: [{ id: null, text: "4", isCorrect: true }, { id: null, text: "5", isCorrect: false }], trueFalseAnswer: null, acceptedAnswers: null, markingGuide: null, topic: "Sums", difficulty: "Easy" });
    expect(draftProblem({ ...v, options: v.options.map((o) => ({ ...o, isCorrect: false })) })).toBe("Mark exactly one option as correct.");
  });
});
