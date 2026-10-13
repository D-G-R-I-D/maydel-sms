import { describe, expect, it } from "vitest";
import {
  addGuardianSchema,
  ageOn,
  guardiansHref,
  looksLikePhone,
  nextStatuses,
  parseStudentSearch,
  phoneKey,
  schoolToday,
  studentSchema,
  studentsHref,
} from "./students";

const today = "2026-10-13";
const base = {
  firstName: "Chidi",
  middleName: "",
  lastName: "Eze",
  dateOfBirth: "2018-03-02",
  sex: "Male",
  stateOfOrigin: "",
  lga: "",
  homeAddress: "",
  house: "",
  medicalNotes: "",
  classRoomId: "",
  admittedOn: "2026-09-14",
};
const errs = (input: object) => {
  const r = studentSchema(today).safeParse(input);
  return r.success ? [] : r.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`);
};

describe("student rules", () => {
  it("accepts a valid admission and nulls blank optionals", () => {
    const r = studentSchema(today).parse(base);
    expect(r.middleName).toBeNull();
    expect(r.classRoomId).toBeNull();
    expect(r.sex).toBe("Male");
  });

  it("requires names and an admission date at most a year ahead", () => {
    expect(errs({ ...base, firstName: " ", lastName: "" }).map((e) => e.split(":")[0])).toEqual(["firstName", "lastName"]);
    expect(errs({ ...base, admittedOn: "2027-10-14" })).toEqual(["admittedOn: Admission can be at most a year ahead."]);
    expect(errs({ ...base, admittedOn: "2027-10-13" })).toEqual([]);
  });

  it("checks date of birth: not future, 1–25 at admission", () => {
    expect(errs({ ...base, dateOfBirth: "2026-12-01" })).toEqual(["dateOfBirth: Date of birth can't be in the future."]);
    expect(errs({ ...base, dateOfBirth: "2026-01-01" })).toEqual(["dateOfBirth: The pupil must be 1–25 years old at admission."]);
    expect(errs({ ...base, dateOfBirth: "2000-01-01" })).toEqual(["dateOfBirth: The pupil must be 1–25 years old at admission."]);
    expect(errs({ ...base, dateOfBirth: "" })).toEqual([]);
  });

  it("knows ages and the school's today", () => {
    expect(ageOn("2018-03-02", "2026-03-01")).toBe(7);
    expect(ageOn("2018-03-02", "2026-03-02")).toBe(8);
    expect(schoolToday(new Date("2026-10-12T23:30:00Z"))).toBe("2026-10-13"); // 00:30 in Lagos
  });
});

describe("guardian rules", () => {
  const g = { fullName: "Ada Eze", phone: "0803 000 0000", email: "", occupation: "", address: "", relationship: "Mother", isPrimaryContact: true, canPickUp: true };

  it("accepts Nigerian phones in any common format", () => {
    for (const p of ["0803 000 0000", "+234 803 000 0000", "(0803) 000-0000", "+2348030000000"]) expect(looksLikePhone(p)).toBe(true);
    expect(looksLikePhone("call me")).toBe(false);
    expect(looksLikePhone("123")).toBe(false);
  });

  it("matches the same phone however it's typed", () => {
    expect(phoneKey("0803 000 0000")).toBe(phoneKey("+234 803 000 0000"));
    expect(phoneKey("(0803) 000-0000")).toBe("8030000000");
  });

  it("validates the add-guardian form", () => {
    expect(addGuardianSchema.safeParse(g).success).toBe(true);
    expect(addGuardianSchema.parse(g).email).toBeNull();
    expect(addGuardianSchema.safeParse({ ...g, email: "nope" }).success).toBe(false);
    expect(addGuardianSchema.safeParse({ ...g, relationship: "Aunt" }).success).toBe(false);
  });
});

describe("status transitions", () => {
  it("allows only the documented moves", () => {
    expect(nextStatuses("Active")).toEqual(["Suspended", "Withdrawn", "Graduated"]);
    expect(nextStatuses("Suspended")).toEqual(["Active", "Withdrawn", "Graduated"]);
    expect(nextStatuses("Withdrawn")).toEqual(["Active"]);
    expect(nextStatuses("Graduated")).toEqual([]);
  });
});

describe("list filters", () => {
  it("round-trips through the URL, defaulting to active students", () => {
    const id = "01a131b3-ade3-7576-93ce-35cbb954e1a4";
    const s = parseStudentSearch({ search: " eze ", classRoomId: id, status: "Suspended", page: "2" });
    expect(studentsHref(s)).toBe(`/admin/students?search=eze&classRoomId=${id}&status=Suspended&page=2`);
    expect(parseStudentSearch({ status: "bogus", classRoomId: "x" })).toEqual({ search: undefined, classRoomId: undefined, status: "Active", page: 1 });
    expect(guardiansHref("0803", 3)).toBe("/admin/guardians?search=0803&page=3");
  });
});
