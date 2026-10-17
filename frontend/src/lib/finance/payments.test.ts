import { describe, expect, it } from "vitest";
import { formatNaira } from "./rules";
import { nextDueDate, onlineAmountError, paymentErrors, scheduleStates, signed } from "./payments";

describe("payments", () => {
  it("shows negatives as −₦", () => {
    expect(signed(-5000, formatNaira)).toBe("−₦5,000.00");
    expect(signed(5000, formatNaira)).toBe("₦5,000.00");
  });

  it("checks amounts like the API", () => {
    const inv = { balance: 100_000, instalmentsAllowed: true, minimumPayment: 40_000 };
    const ok = { amount: "40,000", paidOn: "2026-10-15", reference: "" };
    expect(paymentErrors(ok, inv, "2026-10-15")).toEqual({});
    expect(paymentErrors({ ...ok, amount: "100000.01" }, inv, "2026-10-15").amount).toBe("That's more than the balance.");
    expect(paymentErrors({ ...ok, amount: "0" }, inv, "2026-10-15").amount).toMatch(/above ₦0/);
    expect(paymentErrors(ok, { ...inv, instalmentsAllowed: false }, "2026-10-15").amount).toBe("Full payment only this term: pay the whole balance.");
    expect(paymentErrors({ ...ok, amount: "100,000" }, { ...inv, instalmentsAllowed: false }, "2026-10-15")).toEqual({});
    expect(paymentErrors({ ...ok, paidOn: "2026-10-16" }, inv, "2026-10-15").paidOn).toBe("The payment date can't be in the future.");
    expect(paymentErrors({ ...ok, amount: "39,999.99" }, inv, "2026-10-15").amount).toBe("Pay at least ₦40,000.00 to complete the next instalment, or the full balance of ₦100,000.00.");
    expect(paymentErrors({ ...ok, amount: "100000" }, { ...inv, minimumPayment: 150_000 }, "2026-10-15")).toEqual({});
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

describe("nextDueDate", () => {
  const sched = [
    { order: 1, percent: 60, amount: 60_000, dueOn: "2026-09-15" },
    { order: 2, percent: 40, amount: 40_000, dueOn: "2026-11-15" },
  ];
  it("finds the first part not yet paid, across invoices", () => {
    expect(nextDueDate([{ balance: 40_000, paid: 60_000, dueOn: "2026-11-15", schedule: sched, status: "PartiallyPaid" }], "2026-10-17")).toBe("2026-11-15");
    expect(nextDueDate([{ balance: 100_000, paid: 0, dueOn: "2026-11-15", schedule: sched, status: "Unpaid" }], "2026-10-17")).toBe("2026-09-15");
    expect(nextDueDate([{ balance: 5_000, paid: 0, dueOn: "2026-12-01", schedule: [], status: "Unpaid" }], "2026-10-17")).toBe("2026-12-01");
  });
  it("ignores paid and cancelled invoices", () => {
    expect(nextDueDate([{ balance: 0, paid: 100_000, dueOn: "2026-11-15", schedule: sched, status: "Paid" }], "2026-10-17")).toBeNull();
    expect(nextDueDate([{ balance: 5_000, paid: 0, dueOn: "2026-12-01", schedule: [], status: "Cancelled" }], "2026-10-17")).toBeNull();
  });
});

describe("onlineAmountError", () => {
  const inv = { balance: 100_000, minimumPayment: 60_000 };
  it("allows the balance or at least the minimum", () => {
    expect(onlineAmountError("100,000", inv)).toBeNull();
    expect(onlineAmountError("60000", inv)).toBeNull();
    expect(onlineAmountError("59,999", inv)).toBe("Pay at least ₦60,000.00 to complete the next instalment.");
    expect(onlineAmountError("100,000.01", inv)).toBe("That's more than the balance of ₦100,000.00.");
    expect(onlineAmountError("abc", inv)).toMatch(/above ₦0/);
  });
});
