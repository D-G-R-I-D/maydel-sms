import { describe, expect, it } from "vitest";
import { childSchema, formCodeError, normalizeFormCode, registerSchema } from "./rules";

const child = { childFirstName: "Ada", childLastName: "Eze", childDateOfBirth: "", classLevelId: "l", sessionId: "s" };
const parent = { parentName: "Mrs. Ngozi Eze", email: "ngozi@example.com", phone: "0803 123 4567", password: "Abcdefgh12", website: "" };

describe("admissions forms", () => {
  it("accepts a child without a date of birth", () => {
    expect(childSchema.parse(child)).toEqual({ ...child, childDateOfBirth: null });
  });

  it("checks the parent's account like the API", () => {
    expect(registerSchema.safeParse({ ...child, ...parent }).success).toBe(true);
    const weak = registerSchema.safeParse({ ...child, ...parent, password: "short" });
    expect(weak.success).toBe(false);
    expect(registerSchema.safeParse({ ...child, ...parent, email: "nope" }).success).toBe(false);
    expect(registerSchema.safeParse({ ...child, ...parent, classLevelId: "" }).success).toBe(false);
  });
});

describe("form codes", () => {
  it("normalises case and spacing", () => {
    expect(normalizeFormCode(" abcd efgh ")).toBe("ABCD-EFGH");
    expect(normalizeFormCode("abcd-efgh")).toBe("ABCD-EFGH");
  });
  it("rejects lookalike characters and wrong lengths", () => {
    expect(formCodeError("ABCD-EFGH")).toBeNull();
    expect(formCodeError("ABC0-EFGH")).toMatch(/look like/);
    expect(formCodeError("ABCD-EFG")).toMatch(/look like/);
    expect(formCodeError("")).toMatch(/Enter the form code/);
  });
});
