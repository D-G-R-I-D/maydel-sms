import { describe, expect, it } from "vitest";
import { schoolProfileSchema } from "./school";

const base = { name: "Maydel Spring Schools", motto: "", address: "Abuja", phone: "", email: "", website: "", principalName: "", whatsApp: "", mapUrl: "", facebookUrl: "", instagramUrl: "", officeHours: "" };
const web = { whatsApp: null, mapUrl: null, facebookUrl: null, instagramUrl: null, officeHours: null };

describe("schoolProfileSchema", () => {
  it("stores blank optional fields as null and trims", () => {
    const r = schoolProfileSchema.parse({ ...base, name: "  Maydel  ", motto: "  " });
    expect(r).toEqual({ name: "Maydel", motto: null, address: "Abuja", phone: null, email: null, website: null, principalName: null, ...web });
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

  it("checks the website fields: https links and a WhatsApp number", () => {
    const bad = schoolProfileSchema.safeParse({ ...base, mapUrl: "http://maps.google.com/x", facebookUrl: "facebook.com/x", whatsApp: "call me" });
    expect(bad.success).toBe(false);
    const fields = bad.success ? [] : bad.error.issues.map((i) => i.path[0]);
    expect(fields).toEqual(expect.arrayContaining(["mapUrl", "facebookUrl", "whatsApp"]));
    const ok = schoolProfileSchema.parse({ ...base, mapUrl: " https://maps.app.goo.gl/abc ", whatsApp: "0803 123 4567", officeHours: "Mon–Fri 8–4" });
    expect([ok.mapUrl, ok.whatsApp, ok.officeHours]).toEqual(["https://maps.app.goo.gl/abc", "0803 123 4567", "Mon–Fri 8–4"]);
  });
});
