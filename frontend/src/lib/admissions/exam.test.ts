import { describe, expect, it } from "vitest";
import { cutOffError, formatSlotRange, formatSlotStart, lagosIso, scoreError, slotErrors, toLagosParts } from "./exam";

describe("exam times (Lagos, UTC+1)", () => {
  it("formats UTC instants in Lagos time", () => {
    expect(formatSlotStart("2026-10-24T08:00:00Z")).toMatch(/24 Oct 2026, 09:00/);
    expect(formatSlotRange("2026-10-24T08:00:00Z", "2026-10-24T10:00:00Z")).toMatch(/09:00–11:00$/);
  });
  it("round-trips the sitting form's local date and time", () => {
    expect(lagosIso("2026-10-24", "09:00")).toBe("2026-10-24T09:00:00+01:00");
    expect(toLagosParts("2026-10-24T08:00:00Z")).toEqual({ date: "2026-10-24", time: "09:00" });
    expect(toLagosParts("2026-10-24T23:30:00Z")).toEqual({ date: "2026-10-25", time: "00:30" });
  });
});

describe("sitting form", () => {
  const now = new Date("2026-10-18T12:00:00Z");
  const ok = { date: "2026-10-24", time: "09:00", durationMinutes: "120", venue: "Main hall", mode: "Paper" as const, capacity: "40", notes: "" };
  it("accepts a valid future sitting", () => {
    expect(slotErrors(ok, now)).toEqual({});
  });
  it("rejects past times and bad numbers", () => {
    expect(slotErrors({ ...ok, date: "2026-10-17" }, now).time).toBe("The sitting must start in the future.");
    expect(slotErrors({ ...ok, durationMinutes: "5", capacity: "0", venue: " " }, now)).toEqual({
      durationMinutes: "Enter 15 to 480 minutes.",
      capacity: "Enter 1 to 1,000 seats.",
      venue: "Enter the venue.",
    });
  });
});

describe("scores", () => {
  it("accepts 0–100 with up to two decimals", () => {
    expect(scoreError("72.5")).toBeNull();
    expect(scoreError("0")).toBeNull();
    expect(scoreError("100.5")).toBe("Scores run from 0 to 100.");
    expect(scoreError("72.25")).toBeNull();
    expect(scoreError("72.255")).toBe("Use at most 2 decimal places.");
    expect(scoreError("")).toBe("Enter a score.");
    expect(cutOffError("")).toBeNull();
    expect(cutOffError("101")).toBe("Scores run from 0 to 100.");
  });
});
