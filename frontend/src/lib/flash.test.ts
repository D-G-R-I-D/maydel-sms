import { describe, expect, it } from "vitest";
import { encodeFlash, firstName, flashMessages, parseFlash } from "./flash";

describe("flash toasts", () => {
  it("round-trips and rejects unknown kinds", () => {
    expect(parseFlash(encodeFlash({ kind: "signed-in", name: "Ada" }))).toEqual({ kind: "signed-in", name: "Ada" });
    expect(parseFlash('{"kind":"<script>"}')).toBeNull();
    expect(parseFlash("not json")).toBeNull();
  });

  it("personalises the welcome", () => {
    expect(flashMessages["signed-in"](firstName("Ada Obi")).title).toBe("Welcome back, Ada");
    expect(firstName("Mrs Ngozi Eze")).toBe("Mrs. Eze");
    expect(firstName("Dr. Tunde Bakare")).toBe("Dr. Bakare");
  });
});
