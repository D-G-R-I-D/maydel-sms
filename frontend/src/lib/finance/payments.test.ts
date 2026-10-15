import { describe, expect, it } from "vitest";
import { formatNaira } from "./rules";
import { paymentErrors, scheduleStates, signed } from "./payments";

describe("payments", () => {
  it("shows negatives as −₦", () => {
    expect(signed(-5000, formatNaira)).toBe("−₦5,000.00");
    expect(signed(5000, formatNaira)).toBe("₦5,000.00");
  });

  it("checks amounts like the API", () => {
    const inv = { balance: 100_000, instalmentsAllowed: true };
    const ok = { amount: "40,000", paidOn: "2026-10-15", reference: "" };
    expect(paymentErrors(ok, inv, "2026-10-15")).toEqual({});
    expect(paymentErrors({ ...ok, amount: "100000.01" }, inv, "2026-10-15").amount).toBe("That's more than the balance.");
    expect(paymentErrors({ ...ok, amount: "0" }, inv, "2026-10-15").amount).toMatch(/above ₦0/);
    expect(paymentErrors(ok, { ...inv, instalmentsAllowed: false }, "2026-10-15").amount).toBe("Full payment only this term: pay the whole balance.");
    expect(paymentErrors({ ...ok, amount: "100,000" }, { ...inv, instalmentsAllowed: false }, "2026-10-15")).toEqual({});
    expect(paymentErrors({ ...ok, paidOn: "2026-10-16" }, inv, "2026-10-15").paidOn).toBe("The payment date can't be in the future.");
  });

  it("marks instalments paid, overdue, due or upcoming", () => {
    const schedule = [
      { amount: 60_000, dueOn: "2026-09-15" },
      { amount: 40_000, dueOn: "2026-10-15" },
      { amount: 20_000, dueOn: "2026-11-15" },
    ];
    expect(scheduleStates(schedule, 0, "2026-10-15")).toEqual(["overdue", "due", "upcoming"]);
    expect(scheduleStates(schedule, 60_000, "2026-10-15")).toEqual(["paid", "due", "upcoming"]);
    expect(scheduleStates(schedule, 70_000, "2026-10-20")).toEqual(["paid", "overdue", "upcoming"]);
    expect(scheduleStates(schedule, 120_000, "2026-12-01")).toEqual(["paid", "paid", "paid"]);
  });
});
