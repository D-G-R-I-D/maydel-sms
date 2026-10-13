import { describe, expect, it } from "vitest";
import {
  classroomSchema,
  formatDay,
  groupBySection,
  nextSessionName,
  nextTermNumber,
  sessionSchema,
  subjectSchema,
  termPayload,
  termSchema,
} from "./academics";
import { diffSelection, editableSubjects } from "@/lib/academics/class-subjects";
import type { SubjectDto } from "@/lib/api/types";

const messages = (r: { success: boolean; error?: { issues: { path: PropertyKey[]; message: string }[] } }) =>
  r.success ? [] : r.error!.issues.map((i) => `${i.path.join(".")}: ${i.message}`);

describe("session rules", () => {
  it("accepts two consecutive years that end after they start", () => {
    expect(sessionSchema.safeParse({ name: "2027/2028", startsOn: "2027-09-06", endsOn: "2028-07-21" }).success).toBe(true);
  });

  it("rejects non-consecutive or malformed names and reversed dates", () => {
    expect(messages(sessionSchema.safeParse({ name: "2027/2029", startsOn: "2027-09-06", endsOn: "2028-07-21" }))).toEqual([
      "name: The two years must be consecutive, e.g. 2027/2028.",
    ]);
    expect(messages(sessionSchema.safeParse({ name: "2027-28", startsOn: "2027-09-06", endsOn: "2028-07-21" }))[0]).toMatch(/^name:/);
    expect(messages(sessionSchema.safeParse({ name: "2027/2028", startsOn: "2028-07-21", endsOn: "2027-09-06" }))).toEqual([
      "endsOn: The session must end after it starts.",
    ]);
  });

  it("suggests the next session name", () => {
    expect(nextSessionName("2026/2027")).toBe("2027/2028");
    expect(nextSessionName(undefined, new Date(2026, 9, 13))).toBe("2026/2027");
    expect(nextSessionName(undefined, new Date(2027, 2, 1))).toBe("2026/2027");
  });
});

describe("term rules", () => {
  const schema = termSchema({ sessionStartsOn: "2026-09-07", sessionEndsOn: "2027-07-23" });
  const base = { number: "First" as const, startsOn: "2026-09-07", endsOn: "2026-12-11", midTermStartsOn: "", midTermEndsOn: "" };

  it("accepts a term inside its session, with or without a mid-term break", () => {
    expect(schema.safeParse(base).success).toBe(true);
    expect(schema.safeParse({ ...base, midTermStartsOn: "2026-10-26", midTermEndsOn: "2026-10-30" }).success).toBe(true);
  });

  it("keeps the term inside the session", () => {
    expect(messages(schema.safeParse({ ...base, startsOn: "2026-09-01" }))[0]).toMatch(/^startsOn: Must be on or after the session start/);
    expect(messages(schema.safeParse({ ...base, endsOn: "2027-08-01" }))[0]).toMatch(/^endsOn: Must be on or before the session end/);
  });

  it("needs both mid-term dates, in order, inside the term", () => {
    expect(messages(schema.safeParse({ ...base, midTermStartsOn: "2026-10-26" }))).toEqual(["midTermEndsOn: A mid-term break needs both dates."]);
    expect(messages(schema.safeParse({ ...base, midTermStartsOn: "2026-10-30", midTermEndsOn: "2026-10-26" }))).toContain(
      "midTermEndsOn: The break must end on or after it starts.",
    );
    expect(messages(schema.safeParse({ ...base, midTermStartsOn: "2026-12-14", midTermEndsOn: "2026-12-18" }))).toContain(
      "midTermStartsOn: The break must fall inside the term.",
    );
  });

  it("sends null, not empty strings, for no break", () => {
    expect(termPayload(base)).toEqual({ startsOn: "2026-09-07", endsOn: "2026-12-11", midTermStartsOn: null, midTermEndsOn: null });
  });

  it("offers terms in order", () => {
    expect(nextTermNumber([])).toBe("First");
    expect(nextTermNumber(["First"])).toBe("Second");
    expect(nextTermNumber(["First", "Second", "Third"])).toBeNull();
  });
});

describe("class rules", () => {
  it("requires an arm and a capacity of 1–200", () => {
    expect(classroomSchema().safeParse({ classLevelId: "x", arm: "A", capacity: "30" }).data?.capacity).toBe(30);
    expect(messages(classroomSchema().safeParse({ classLevelId: "x", arm: " ", capacity: 0 })).map((m) => m.split(":")[0])).toEqual(["arm", "capacity"]);
    expect(classroomSchema().safeParse({ classLevelId: "x", arm: "A".repeat(33), capacity: 30 }).success).toBe(false);
    expect(classroomSchema().safeParse({ classLevelId: "x", arm: "A", capacity: 201 }).success).toBe(false);
  });

  it("won't shrink capacity below the students already enrolled", () => {
    expect(messages(classroomSchema(24).safeParse({ classLevelId: "x", arm: "A", capacity: 20 }))).toEqual(["capacity: The class already has 24 students."]);
  });

  it("groups classes by section in level order", () => {
    const rows = [
      { section: "Primary" as const, levelOrder: 7, name: "Year 3 B" },
      { section: "Creche" as const, levelOrder: 1, name: "Creche A" },
      { section: "Primary" as const, levelOrder: 7, name: "Year 3 A" },
      { section: "Primary" as const, levelOrder: 5, name: "Year 1 A" },
    ];
    expect(groupBySection(rows).map((g) => [g.label, g.rows.map((r) => r.name)])).toEqual([
      ["Creche", ["Creche A"]],
      ["Primary", ["Year 1 A", "Year 3 A", "Year 3 B"]],
    ]);
  });
});

describe("subject rules", () => {
  it("upper-cases codes and allows only letters and digits", () => {
    expect(subjectSchema.parse({ name: "Mathematics", code: "mth1", isActive: true }).code).toBe("MTH1");
    expect(subjectSchema.safeParse({ name: "Maths", code: "MTH-1", isActive: true }).success).toBe(false);
    expect(subjectSchema.safeParse({ name: "Maths", code: "ABCDEFGHIJK", isActive: true }).success).toBe(false);
  });
});

describe("class subjects", () => {
  const s = (id: string, name: string, isActive = true): SubjectDto => ({ id, name, code: id.toUpperCase(), isActive });
  const all = [s("eng", "English"), s("mth", "Mathematics"), s("lat", "Latin", false), s("fre", "French", false)];
  const assigned = [{ id: "cs1", subjectId: "fre", subject: "French", code: "FRE", teacherId: null, teacher: null }];

  it("lists active subjects plus inactive ones the class already offers", () => {
    expect(editableSubjects(all, assigned).map((x) => x.id)).toEqual(["eng", "fre", "mth"]);
  });

  it("reports what will be added and removed", () => {
    const d = diffSelection(["fre", "eng"], new Set(["eng", "mth"]));
    expect([...d.added]).toEqual(["mth"]);
    expect([...d.removed]).toEqual(["fre"]);
    expect(d.dirty).toBe(true);
    expect(diffSelection(["eng"], new Set(["eng"])).dirty).toBe(false);
  });

  it("formats calendar days without timezone drift", () => {
    expect(formatDay("2026-09-07")).toMatch(/7 Sept? 2026/);
  });
});
