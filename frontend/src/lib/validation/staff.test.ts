import { describe, expect, it } from "vitest";
import { isSelf, parseStaffSearch, staffHref, staffSchema, teacherOptions } from "./staff";
import { changedTeachers } from "@/lib/academics/class-subjects";

const valid = { fullName: "Mrs. Grace Okafor", email: "grace@maydel.test", phone: "", jobTitle: "", roles: ["Teacher"] as const, isActive: true };
const errs = (input: object) => {
  const r = staffSchema.safeParse(input);
  return r.success ? [] : r.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`);
};

describe("staff rules", () => {
  it("accepts a valid record and turns blank optionals into null", () => {
    const r = staffSchema.parse({ ...valid, roles: ["Teacher"] });
    expect(r.phone).toBeNull();
    expect(r.jobTitle).toBeNull();
  });

  it("checks email, phone, lengths and roles", () => {
    expect(errs({ ...valid, email: "not-an-email" })).toEqual(["email: Enter a valid email address."]);
    expect(errs({ ...valid, phone: "abc" })[0]).toMatch(/^phone:/);
    expect(staffSchema.safeParse({ ...valid, phone: "+234 (803) 123-4567" }).success).toBe(true);
    expect(errs({ ...valid, fullName: "x".repeat(151) })[0]).toMatch(/^fullName:/);
    expect(errs({ ...valid, jobTitle: "x".repeat(101) })[0]).toMatch(/^jobTitle:/);
    expect(errs({ ...valid, roles: [] })).toEqual(["roles: Choose at least one role."]);
    expect(staffSchema.safeParse({ ...valid, roles: ["Parent"] }).success).toBe(false);
  });

  it("recognises your own record by email (case-insensitive)", () => {
    expect(isSelf("Admin@Maydel.test", "admin@maydel.test")).toBe(true);
    expect(isSelf("grace@maydel.test", "admin@maydel.test")).toBe(false);
  });

  it("round-trips list filters through the URL", () => {
    const s = parseStaffSearch({ search: " okafor ", status: "inactive", page: "2" });
    expect(s).toEqual({ search: "okafor", status: "inactive", page: 2 });
    expect(staffHref(s)).toBe("/admin/staff?search=okafor&status=inactive&page=2");
    expect(staffHref(parseStaffSearch({}))).toBe("/admin/staff");
  });
});

describe("teacher pickers", () => {
  const lookup = [
    { id: "t1", fullName: "Mrs. Grace Okafor", jobTitle: "Class teacher" },
    { id: "t2", fullName: "Mr. Tunde Bello", jobTitle: null },
  ];

  it("labels options with job titles", () => {
    expect(teacherOptions(lookup, { id: null, name: null }).map((o) => o.label)).toEqual(["Mrs. Grace Okafor · Class teacher", "Mr. Tunde Bello"]);
  });

  it("keeps a deactivated current teacher selectable, marked inactive", () => {
    expect(teacherOptions(lookup, { id: "t9", name: "Mr. Old" })[0]).toEqual({ id: "t9", label: "Mr. Old (inactive)" });
  });

  it("counts teacher changes only on subjects the class keeps", () => {
    const saved = [
      { id: "1", subjectId: "eng", subject: "English", code: "ENG", teacherId: "t1", teacher: "Okafor" },
      { id: "2", subjectId: "mth", subject: "Maths", code: "MTH", teacherId: null, teacher: null },
    ];
    expect([...changedTeachers(saved, { eng: "t2", mth: null }, new Set(["eng", "mth"]))]).toEqual(["eng"]);
    expect([...changedTeachers(saved, { eng: "t2" }, new Set(["mth"]))]).toEqual([]);
  });
});
