import { describe, expect, it } from "vitest";
import { addDays, cleanCode, hhmm, hours, isDate, resolveKioskUrl, resultHeadline, toApiTime, todayWat, watTime, type ClockResult } from "./clock";

const day = (over: Partial<ClockResult["day"]> = {}): ClockResult["day"] => ({
  staffId: "s1",
  name: "Ada Obi",
  jobTitle: null,
  date: "2026-10-26",
  clockInAt: "2026-10-26T06:42:00Z",
  clockOutAt: null,
  isLate: false,
  state: "In",
  hoursWorked: null,
  inMethod: "Qr",
  outMethod: null,
  note: null,
  ...over,
});

describe("staff clock-in helpers", () => {
  it("shows instants in Abuja time", () => {
    expect(watTime("2026-10-26T06:42:00Z")).toBe("07:42");
    expect(watTime(null)).toBe("—");
  });

  it("takes today's date in WAT, not UTC", () => {
    expect(todayWat(new Date("2026-10-25T23:30:00Z"))).toBe("2026-10-26");
    expect(addDays("2026-10-01", -1)).toBe("2026-09-30");
  });

  it("accepts only real calendar dates", () => {
    expect(isDate("2026-10-26")).toBe(true);
    expect(isDate("2026-13-01")).toBe(false);
    expect(isDate("26/10/2026")).toBe(false);
    expect(isDate(undefined)).toBe(false);
  });

  it("writes the result card headline", () => {
    expect(resultHeadline({ clockedIn: true, day: day() })).toEqual({ title: "Clocked in at 07:42", detail: "On time. Have a good day." });
    expect(resultHeadline({ clockedIn: true, day: day({ isLate: true }) }).detail).toMatch(/late/);
    expect(resultHeadline({ clockedIn: false, day: day({ clockOutAt: "2026-10-26T15:05:00Z", hoursWorked: 8.38 }) })).toEqual({
      title: "Clocked out at 16:05",
      detail: "8.4 h today. See you tomorrow.",
    });
  });

  it("resolves the kiosk URL against the page and refuses other schemes", () => {
    expect(resolveKioskUrl("/clock?c=123.ab", "http://localhost:3000")).toBe("http://localhost:3000/clock?c=123.ab");
    expect(resolveKioskUrl("https://portal.example.ng/clock?c=1", "http://localhost:3000")).toBe("https://portal.example.ng/clock?c=1");
    expect(resolveKioskUrl("javascript:alert(1)", "http://localhost:3000")).toBeNull();
  });

  it("only passes through well-formed scan codes", () => {
    expect(cleanCode("58123456.A1B2C3D4E5F6")).toBe("58123456.A1B2C3D4E5F6");
    expect(cleanCode(["abc.def", "x"])).toBe("abc.def");
    expect(cleanCode("<script>")).toBeNull();
    expect(cleanCode(undefined)).toBeNull();
  });

  it("formats times and hours", () => {
    expect(toApiTime("07:30")).toBe("07:30:00");
    expect(toApiTime("24:00")).toBeNull();
    expect(hhmm("07:30:00")).toBe("07:30");
    expect(hours(8)).toBe("8 h");
    expect(hours(null)).toBe("—");
  });
});
