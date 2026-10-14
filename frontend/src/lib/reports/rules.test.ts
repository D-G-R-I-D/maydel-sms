import { describe, expect, it } from "vitest";
import { attendanceErrors, attendanceText, formatAverage, neighbours, ordinal, publishPreview } from "./rules";
import type { ClassReportSummary } from "@/lib/api/types";

const row = (id: string, scored: number, daysPresent: number | null) => ({
  studentId: id,
  admissionNumber: id,
  name: id,
  subjectsScored: scored,
  average: null,
  position: null,
  status: "Draft" as const,
  daysPresent,
  hasFormTeacherComment: false,
  hasPrincipalComment: false,
  traitsRated: 0,
});
const summary = (daysOpened: number | null, rows: ReturnType<typeof row>[]): ClassReportSummary => ({
  classRoomId: "c",
  classRoom: "Year 3 A",
  termId: "t",
  term: "First Term",
  formTeacher: null,
  subjectsOffered: 9,
  classAverage: 0,
  daysOpened,
  publishedCount: 0,
  draftCount: rows.length,
  rows,
});

describe("report helpers", () => {
  it("writes positions as ordinals", () => {
    expect([1, 2, 3, 4, 11, 12, 13, 21, 22, 23, 101, 111].map(ordinal)).toEqual(["1st", "2nd", "3rd", "4th", "11th", "12th", "13th", "21st", "22nd", "23rd", "101st", "111th"]);
  });

  it("formats averages to 2 dp", () => {
    expect(formatAverage(64.5)).toBe("64.50");
    expect(formatAverage(null)).toBe("—");
  });

  it("previews a publish: skipped without scores, blocked without attendance", () => {
    const p = publishPreview(summary(60, [row("a", 9, 58), row("b", 0, null), row("c", 3, null)]));
    expect(p.willPublish).toBe(2);
    expect(p.noScores.map((r) => r.studentId)).toEqual(["b"]);
    expect(p.missingAttendance.map((r) => r.studentId)).toEqual(["c"]);
    expect(publishPreview(summary(null, [row("a", 9, 58)])).missingAttendance).toHaveLength(1);
  });

  it("shows attendance as present/opened", () => {
    expect(attendanceText({ daysPresent: 58 }, 60)).toBe("58/60");
    expect(attendanceText({ daysPresent: null }, 60)).toBe("—");
  });

  it("validates attendance like the API", () => {
    expect(attendanceErrors("60", { a: "58", b: "60" })).toEqual({});
    expect(attendanceErrors("0", {})).toEqual({ daysOpened: "Enter a whole number from 1 to 200." });
    expect(attendanceErrors("60", { a: "61", b: "", c: "5.5" })).toEqual({ a: "At most 60.", b: "Required.", c: "Whole days only." });
  });

  it("finds prev/next students", () => {
    const rows = [{ studentId: "a" }, { studentId: "b" }, { studentId: "c" }];
    expect(neighbours(rows, "a")).toEqual({ prev: null, next: { studentId: "b" }, index: 0 });
    expect(neighbours(rows, "c").next).toBeNull();
  });
});

describe("term comparison helpers", () => {
  it("formats changes with signs", async () => {
    const { formatChange, termShort, reportPdfHref, classPdfHref } = await import("./rules");
    expect(formatChange(5.5)).toBe("+5.5");
    expect(formatChange(-3)).toBe("−3");
    expect(formatChange(0.001)).toBe("0");
    expect(formatChange(null)).toBe("—");
    expect(termShort("Second")).toBe("2nd T");
    expect(reportPdfHref("s", "t")).toBe("/api/media/reports/s/t/pdf");
    expect(classPdfHref("c", "t")).toBe("/api/media/reports/class/pdf?classRoomId=c&termId=t");
  });
});
