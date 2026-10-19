import { describe, expect, it } from "vitest";
import { facilityTone, markError, percentOf, releaseBlocker } from "./marking";

describe("marking", () => {
  it("checks a typed mark against the question's marks", () => {
    expect(markError("", 5)).toBe("Enter a mark.");
    expect(markError("2.555", 5)).toMatch(/2 decimals/);
    expect(markError("-1", 5)).toMatch(/2 decimals/);
    expect(markError("6", 5)).toMatch(/most this question can get is 5/);
    expect(markError("4.5", 5)).toBeNull();
    expect(markError("0", 5)).toBeNull();
  });

  it("explains why results can't be released yet", () => {
    const base = { released: false, closesAt: "2026-10-19T10:00:00Z", now: Date.parse("2026-10-19T11:00:00Z"), sitting: 0, needsMarking: 0, submitted: 3 };
    expect(releaseBlocker(base)).toBeNull();
    expect(releaseBlocker({ ...base, now: Date.parse("2026-10-19T09:00:00Z") })).toMatch(/still open/);
    expect(releaseBlocker({ ...base, sitting: 1 })).toBe("1 student is still sitting.");
    expect(releaseBlocker({ ...base, needsMarking: 4 })).toBe("4 answers need marking first.");
    expect(releaseBlocker({ ...base, submitted: 0 })).toMatch(/Nobody/);
    expect(releaseBlocker({ ...base, released: true })).toMatch(/released/);
  });

  it("percent and facility tones", () => {
    expect(percentOf(7, 20)).toBe(35);
    expect(percentOf(1, 3)).toBe(33.3);
    expect(percentOf(1, 0)).toBe(0);
    expect([facilityTone(0.1), facilityTone(0.3), facilityTone(0.8), facilityTone(null)]).toEqual(["danger", "warning", "success", "neutral"]);
  });
});
