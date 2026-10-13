import { describe, expect, it } from "vitest";
import { problemMessage, toProblem } from "./problem";
import { applyFieldErrors } from "@/lib/client/bff";

describe("toProblem", () => {
  it("keeps code, detail, traceId and field errors from the API", () => {
    const p = toProblem(400, {
      status: 400,
      title: "One or more validation errors occurred.",
      code: "validation",
      traceId: "00-abc-01",
      errors: { newPassword: ["Too short."], currentPassword: ["Required."] },
    });
    expect(p).toMatchObject({ status: 400, code: "validation", traceId: "00-abc-01" });
    expect(p.errors?.newPassword).toEqual(["Too short."]);
  });

  it("falls back to a friendly title for empty or non-JSON bodies", () => {
    expect(toProblem(401, null)).toMatchObject({ status: 401, code: undefined });
    expect(toProblem(429, "<html>").title).toMatch(/too many attempts/i);
    expect(toProblem(502, undefined).title).toBe("Something went wrong.");
  });

  it("prefers detail for users", () => {
    expect(problemMessage(toProblem(401, { title: "Unauthorized", detail: "Wrong username or password." }))).toBe("Wrong username or password.");
    expect(problemMessage(toProblem(429, {}))).toMatch(/wait a minute/);
  });
});

describe("applyFieldErrors", () => {
  it("maps camelCase (and differently-cased) keys to form fields", () => {
    const calls: [string, string][] = [];
    const applied = applyFieldErrors(
      toProblem(400, { errors: { NewPassword: ["Needs a digit."], unknown: ["x"] } }),
      ["currentPassword", "newPassword"] as const,
      (field, e) => calls.push([field, e.message]),
    );
    expect(applied).toBe(true);
    expect(calls).toEqual([["newPassword", "Needs a digit."]]);
  });

  it("ignores non-validation problems", () => {
    expect(applyFieldErrors(toProblem(401, {}), ["userName"] as const, () => {})).toBe(false);
  });
});
