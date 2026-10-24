import { describe, expect, it } from "vitest";
import { ago, badgeText, safeLink } from "./bell";

describe("notification bell helpers", () => {
  it("caps the badge and hides it at zero", () => {
    expect(badgeText(0)).toBeNull();
    expect(badgeText(7)).toBe("7");
    expect(badgeText(150)).toBe("99+");
  });

  it("follows same-site links only", () => {
    expect(safeLink("/portal/fees")).toBe("/portal/fees");
    expect(safeLink("//evil.example")).toBeNull();
    expect(safeLink("https://evil.example")).toBeNull();
    expect(safeLink(null)).toBeNull();
  });

  it("says how long ago, then the date", () => {
    const now = new Date("2026-10-24T12:00:00Z");
    expect(ago("2026-10-24T11:59:30Z", now)).toBe("just now");
    expect(ago("2026-10-24T11:55:00Z", now)).toBe("5 minutes ago");
    expect(ago("2026-10-23T12:00:00Z", now)).toBe("yesterday");
    expect(ago("2026-10-01T12:00:00Z", now)).toBe("1 Oct");
  });
});
