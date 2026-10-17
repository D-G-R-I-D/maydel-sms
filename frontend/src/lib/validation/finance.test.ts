import { describe, expect, it } from "vitest";
import { feeItemSchema } from "./finance";

const base = { name: "Tuition", amount: "150,000", classLevelId: "", isMandatory: true, siblingDiscount: true, audience: "All" as const, chargedByDefault: false };

describe("feeItemSchema", () => {
  it("parses naira amounts and school-wide items", () => {
    expect(feeItemSchema.parse(base)).toEqual({ name: "Tuition", amount: 150000, classLevelId: null, isMandatory: true, siblingDiscount: true, audience: "All", chargedByDefault: false });
  });
  it("rejects zero, negative, too many decimals and huge amounts", () => {
    for (const amount of ["0", "-1", "10.123", "100000001", "abc"]) expect(feeItemSchema.safeParse({ ...base, amount }).success).toBe(false);
  });
  it("needs a name of 80 characters or fewer", () => {
    expect(feeItemSchema.safeParse({ ...base, name: " " }).success).toBe(false);
    expect(feeItemSchema.safeParse({ ...base, name: "x".repeat(81) }).success).toBe(false);
  });
});
