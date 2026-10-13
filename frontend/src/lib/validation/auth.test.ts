import { describe, expect, it } from "vitest";
import { changePasswordSchema, loginSchema, passwordStrength } from "./auth";

const base = { currentPassword: "Demo#Pass2026", newPassword: "Spring2026Term", confirmPassword: "Spring2026Term" };

function issues(input: object) {
  const r = changePasswordSchema.safeParse(input);
  return r.success ? [] : r.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`);
}

describe("password rules (mirror the API)", () => {
  it("accepts a compliant new password", () => {
    expect(issues(base)).toEqual([]);
  });

  it.each([
    ["Short1A", "at least 10"],
    ["alllowercase123", "upper-case"],
    ["ALLUPPERCASE123", "lower-case"],
    ["NoDigitsHereAtAll", "number"],
    [`Aa1${"x".repeat(126)}`, "128"],
  ])("rejects %s", (pw, message) => {
    expect(issues({ ...base, newPassword: pw, confirmPassword: pw }).join(" ")).toContain(message);
  });

  it("requires a password different from the current one", () => {
    expect(issues({ ...base, newPassword: base.currentPassword, confirmPassword: base.currentPassword })).toEqual([
      "newPassword: Choose a password different from your current one.",
    ]);
  });

  it("requires the confirmation to match", () => {
    expect(issues({ ...base, confirmPassword: "Spring2026Tern" })).toEqual(["confirmPassword: The passwords don't match."]);
  });
});

describe("login", () => {
  it("accepts an admission number with slashes and trims it", () => {
    const r = loginSchema.parse({ userName: "  MSS/2026/0001 ", password: "x" });
    expect(r.userName).toBe("MSS/2026/0001");
  });

  it("requires both fields", () => {
    expect(loginSchema.safeParse({ userName: "", password: "" }).success).toBe(false);
  });
});

describe("strength meter", () => {
  it("scores policy failures as weak and long varied passwords as strong", () => {
    expect(passwordStrength("").score).toBe(0);
    expect(passwordStrength("short").label).toBe("Weak");
    expect(passwordStrength("Abcdefgh12").label).toBe("Good");
    expect(passwordStrength("Spring-Term2026!").label).toBe("Strong");
    expect(passwordStrength("Password12345").score).toBeLessThan(4);
  });
});
