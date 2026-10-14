import { describe, expect, it } from "vitest";
import { decideRestore, draftTime } from "./rules";

const draft = (value: unknown, base: string | null = "b1") => ({ value, savedAt: 0, base });

describe("decideRestore", () => {
  it("restores an unsaved edit made against the same server state", () => {
    expect(decideRestore(draft("new"), { base: "b1", editable: true, value: "old" })).toBe("restore");
  });
  it("flags a draft whose server state changed underneath", () => {
    expect(decideRestore(draft("new"), { base: "b2", editable: true, value: "other" })).toBe("restore-changed");
  });
  it("drops a draft on a screen that is now read-only", () => {
    expect(decideRestore(draft("new"), { base: "b1", editable: false, value: "old" })).toBe("drop");
  });
  it("drops a draft equal to what's saved", () => {
    expect(decideRestore(draft({ a: [1] }), { base: "b2", editable: true, value: { a: [1] } })).toBe("drop");
  });
  it("does nothing without a draft", () => {
    expect(decideRestore(null, { base: null, editable: true, value: 1 })).toBe("none");
  });
});

describe("draftTime", () => {
  it("shows only the time for today and the date otherwise (Lagos)", () => {
    const now = Date.UTC(2026, 9, 14, 12, 0);
    expect(draftTime(Date.UTC(2026, 9, 14, 9, 5), now)).toBe("10:05");
    expect(draftTime(Date.UTC(2026, 9, 13, 21, 40), now)).toBe("13 Oct, 22:40");
  });
});
