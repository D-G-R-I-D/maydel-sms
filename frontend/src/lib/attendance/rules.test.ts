import { describe, expect, it } from "vitest";
import { attendanceTone, countMarks, formatRegisterDate, isIsoDate, marksDirty, marksFrom, noteErrors, registerPayload, shiftSchoolDay } from "./rules";

const entry = (id: string, status: "Present" | "Absent" | "Late" | "Excused" = "Present", locked = false, note: string | null = null) => ({
  studentId: id,
  admissionNumber: id,
  name: id,
  status,
  note,
  recorded: false,
  locked,
});

describe("attendance rules", () => {
  it("validates ISO dates", () => {
    expect(isIsoDate("2026-10-15")).toBe(true);
    expect(isIsoDate("2026-02-30")).toBe(false);
    expect(isIsoDate("15/10/2026")).toBe(false);
    expect(isIsoDate(undefined)).toBe(false);
  });

  it("steps over weekends", () => {
    expect(shiftSchoolDay("2026-10-15", 1)).toBe("2026-10-16"); // Thu → Fri
    expect(shiftSchoolDay("2026-10-16", 1)).toBe("2026-10-19"); // Fri → Mon
    expect(shiftSchoolDay("2026-10-19", -1)).toBe("2026-10-16"); // Mon → Fri
  });

  it("formats the day", () => {
    expect(formatRegisterDate("2026-10-15")).toBe("Thursday, 15 October 2026");
  });

  it("highlights low attendance", () => {
    expect(attendanceTone(95)).toBeNull();
    expect(attendanceTone(89.9)).toBe("warning");
    expect(attendanceTone(74)).toBe("danger");
    expect(attendanceTone(null)).toBeNull();
  });

  it("tracks changes, ignoring notes on present students", () => {
    const saved = marksFrom({ entries: [entry("a"), entry("b", "Absent", false, "Sick")] });
    expect(marksDirty(saved, saved)).toBe(false);
    expect(marksDirty({ ...saved, a: { status: "Present", note: "stray" } }, saved)).toBe(false);
    expect(marksDirty({ ...saved, a: { status: "Late", note: "" } }, saved)).toBe(true);
    expect(marksDirty({ ...saved, b: { status: "Absent", note: "Sick " } }, saved)).toBe(false);
    expect(countMarks(saved)).toEqual({ Present: 1, Absent: 1, Late: 0, Excused: 0 });
  });

  it("sends every unlocked student, with notes only when not present", () => {
    const reg = { entries: [entry("a"), entry("b"), entry("c", "Absent", true)] };
    const marks = { a: { status: "Present" as const, note: "x" }, b: { status: "Late" as const, note: " Bus " }, c: { status: "Absent" as const, note: "" } };
    expect(registerPayload(reg, marks)).toEqual([
      { studentId: "a", status: "Present", note: null },
      { studentId: "b", status: "Late", note: "Bus" },
    ]);
  });

  it("limits notes", () => {
    expect(noteErrors({ a: { status: "Absent", note: "x".repeat(201) }, b: { status: "Present", note: "x".repeat(300) } })).toEqual({ a: "Use 200 characters or fewer." });
  });
});
