import { describe, expect, it } from "vitest";
import { schoolProfileSchema } from "./school";

const base = { name: "Maydel Spring Schools", motto: "", address: "Abuja", phone: "", email: "", website: "", principalName: "" };

describe("schoolProfileSchema", () => {
  it("stores blank optional fields as null and trims", () => {
    const r = schoolProfileSchema.parse({ ...base, name: "  Maydel  ", motto: "  " });
    expect(r).toEqual({ name: "Maydel", motto: null, address: "Abuja", phone: null, email: null, website: null, principalName: null });
  });

  it("requires name and address", () => {
    const r = schoolProfileSchema.safeParse({ ...base, name: " ", address: "" });
    expect(r.success).toBe(false);
    expect(r.error?.issues.map((i) => i.path[0]).sort()).toEqual(["address", "name"]);
  });

  it("checks lengths like the API", () => {
    expect(schoolProfileSchema.safeParse({ ...base, name: "x".repeat(121) }).success).toBe(false);
    expect(schoolProfileSchema.safeParse({ ...base, address: "x".repeat(301) }).success).toBe(false);
    expect(schoolProfileSchema.safeParse({ ...base, principalName: "x".repeat(81) }).success).toBe(false);
    expect(schoolProfileSchema.safeParse({ ...base, principalName: "x".repeat(80) }).success).toBe(true);
  });

  it("validates email only when given", () => {
    expect(schoolProfileSchema.safeParse({ ...base, email: "not-an-email" }).success).toBe(false);
    expect(schoolProfileSchema.parse({ ...base, email: "info@maydel.sch.ng" }).email).toBe("info@maydel.sch.ng");
  });
});
