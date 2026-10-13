import { describe, expect, it } from "vitest";
import {
  backupKey,
  buildEntries,
  changedRows,
  draftsFromSheet,
  gradeFor,
  mapServerErrors,
  nextCell,
  parseBackup,
  parseScore,
  rowErrors,
  rowTotal,
  type Component,
  type Drafts,
} from "./grid";
import type { GradeSheet } from "@/lib/api/types";

const comps: Component[] = [
  { id: "ca", name: "CAT", maxScore: 30 },
  { id: "ex", name: "Exams", maxScore: 40 },
  { id: "pj", name: "Project", maxScore: 10 },
];
const bands = [
  { grade: "A", minScore: 70, maxScore: 100, remark: "Excellent" },
  { grade: "B", minScore: 60, maxScore: 69.99, remark: "Very good" },
  { grade: "F", minScore: 0, maxScore: 39.99, remark: "Needs improvement" },
  { grade: "C", minScore: 40, maxScore: 59.99, remark: "Good" },
];
const sheet: GradeSheet = {
  classRoomId: "c",
  classRoom: "Year 3 A",
  subjectId: "s",
  subject: "Maths",
  termId: "t",
  term: "First Term",
  components: comps,
  rows: [
    { studentId: "s1", admissionNumber: "1", name: "Eze, Chidi", scores: [{ componentId: "ca", score: 25 }, { componentId: "ex", score: 33.5 }, { componentId: "pj", score: null }], total: 58.5, grade: "C", remark: "Good", comment: null, locked: false },
    { studentId: "s2", admissionNumber: "2", name: "Obi, Ada", scores: [{ componentId: "ca", score: null }, { componentId: "ex", score: null }, { componentId: "pj", score: null }], total: null, grade: null, remark: null, comment: null, locked: true },
  ],
};

describe("cell parsing", () => {
  it("accepts 0..max with up to 2 decimals; blank is null", () => {
    expect(parseScore("12.5", 30)).toEqual({ value: 12.5, error: null });
    expect(parseScore(" 0 ", 30)).toEqual({ value: 0, error: null });
    expect(parseScore("", 30)).toEqual({ value: null, error: null });
    expect(parseScore("30", 30).error).toBeNull();
  });
  it("explains what's wrong", () => {
    expect(parseScore("31", 30).error).toBe("Max is 30.");
    expect(parseScore("1.234", 30).error).toBe("Use at most 2 decimal places.");
    expect(parseScore("-1", 30).error).toBe("Enter a number.");
    expect(parseScore("abc", 30).error).toBe("Enter a number.");
  });
});

describe("totals and grades", () => {
  const drafts = draftsFromSheet(sheet);
  it("totals valid cells and ignores invalid ones", () => {
    expect(rowTotal(drafts.s1, comps)).toBe(58.5);
    expect(rowTotal({ ...drafts.s1, scores: { ...drafts.s1.scores, pj: "99" } }, comps)).toBe(58.5);
    expect(rowTotal(drafts.s2, comps)).toBeNull();
  });
  it("grades from bands regardless of their order", () => {
    expect(gradeFor(58.5, bands)?.grade).toBe("C");
    expect(gradeFor(70, bands)?.grade).toBe("A");
    expect(gradeFor(0, bands)?.grade).toBe("F");
    expect(gradeFor(null, bands)).toBeNull();
  });
  it("flags invalid cells and long comments", () => {
    expect(rowErrors({ scores: { ca: "31", ex: "x" }, comment: "c".repeat(1001) }, comps)).toEqual({
      ca: "Max is 30.",
      ex: "Enter a number.",
      comment: "Keep comments under 1000 characters.",
    });
  });
});

describe("autosave payload", () => {
  const saved = draftsFromSheet(sheet);
  it("only sends rows that changed (numerically)", () => {
    const drafts: Drafts = { ...saved, s1: { ...saved.s1, scores: { ...saved.s1.scores, ca: "25.0" } } };
    expect(changedRows(drafts, saved, comps)).toEqual([]);
    drafts.s1 = { ...saved.s1, scores: { ...saved.s1.scores, pj: "8" } };
    expect(changedRows(drafts, saved, comps)).toEqual(["s1"]);
  });
  it("builds entries in component order, nulls for blanks, skips invalid rows", () => {
    const drafts = {
      s1: { scores: { ca: "25", ex: "33.5", pj: "8" }, comment: "  Great work " },
      s2: { scores: { ca: "99" }, comment: "" },
      s3: { scores: {}, comment: "" },
    };
    expect(buildEntries(["s1", "s2", "s3"], drafts, comps)).toEqual([
      { studentId: "s1", scores: [{ componentId: "ca", score: 25 }, { componentId: "ex", score: 33.5 }, { componentId: "pj", score: 8 }], comment: "Great work" },
      { studentId: "s3", scores: [{ componentId: "ca", score: null }, { componentId: "ex", score: null }, { componentId: "pj", score: null }], comment: null },
    ]);
  });
  it("maps server error keys back to cells via the request's entries", () => {
    const entries = buildEntries(["s1", "s3"], { s1: { scores: { ca: "1" }, comment: "" }, s3: { scores: {}, comment: "" } }, comps);
    expect(
      mapServerErrors(
        { "entries[1].scores.ex": ["Too high."], "entries[0].studentId": ["Not in this class."], "entries[0].comment": ["Too long."], "entries[9].scores.ca": ["?"], other: ["x"] },
        entries,
      ),
    ).toEqual({ "s3:ex": "Too high.", "s1:row": "Not in this class.", "s1:comment": "Too long." });
  });
});

describe("keyboard navigation", () => {
  const size = { rows: 4, cols: 3 };
  it("moves within bounds", () => {
    expect(nextCell({ row: 0, col: 0 }, "right", size)).toEqual({ row: 0, col: 1 });
    expect(nextCell({ row: 0, col: 2 }, "right", size)).toBeNull();
    expect(nextCell({ row: 0, col: 0 }, "left", size)).toBeNull();
    expect(nextCell({ row: 3, col: 1 }, "down", size)).toBeNull();
  });
  it("skips locked rows going up and down", () => {
    const editable = (r: number) => r !== 1 && r !== 2;
    expect(nextCell({ row: 0, col: 1 }, "down", size, editable)).toEqual({ row: 3, col: 1 });
    expect(nextCell({ row: 3, col: 1 }, "up", size, editable)).toEqual({ row: 0, col: 1 });
  });
});

describe("session backup", () => {
  it("is keyed per sheet and survives junk", () => {
    expect(backupKey("c", "s", "t")).toBe("maydel.gradesheet.v1.c.s.t");
    expect(parseBackup(JSON.stringify({ at: 5, rows: { s1: { scores: { ca: "3", bad: 4 }, comment: "x" }, s2: "nope" } }))).toEqual({
      at: 5,
      rows: { s1: { scores: { ca: "3" }, comment: "x" } },
    });
    expect(parseBackup("{not json")).toBeNull();
    expect(parseBackup(null)).toBeNull();
  });
});
