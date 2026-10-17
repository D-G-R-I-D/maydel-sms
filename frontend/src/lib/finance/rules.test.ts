import { describe, expect, it } from "vitest";
import type { FeeItem } from "@/lib/api/types";
import { formatNaira, formatNairaShort, generateToast, groupFeeItems, groupTotal, instalmentErrors, instalmentTotal, parseAmount, siblingErrors, siblingPays } from "./rules";

const item = (name: string, level: string | null, mandatory = true, amount = 1000): FeeItem => ({
  id: name,
  termId: "t",
  name,
  amount,
  classLevelId: level,
  classLevel: level ? `Level ${level}` : null,
  isMandatory: mandatory,
  siblingDiscount: false,
  optedIn: 0,
  audience: "All",
  chargedByDefault: false,
  optedOut: 0,
});

describe("money", () => {
  it("formats naira with kobo", () => {
    expect(formatNaira(150000)).toBe("₦150,000.00");
    expect(formatNaira(0.5)).toBe("₦0.50");
    expect(formatNairaShort(18_400_000)).toBe("₦18.4M");
  });

  it("parses typed amounts", () => {
    expect(parseAmount("150,000")).toBe(150000);
    expect(parseAmount(" ₦1,500.5 ")).toBe(1500.5);
    expect(parseAmount("12.345")).toBeNull();
    expect(parseAmount("-5")).toBeNull();
    expect(parseAmount("")).toBeNull();
  });
});

describe("fee items", () => {
  it("groups school-wide first, mandatory before optional", () => {
    const groups = groupFeeItems([item("Bus", "1", false), item("Tuition", "1"), item("Uniform", null)]);
    expect(groups.map((g) => g.label)).toEqual(["All classes", "Level 1"]);
    expect(groups[1].items.map((i) => i.name)).toEqual(["Tuition", "Bus"]);
    expect(groupTotal(groups[1].items)).toBe(1000);
  });
});

describe("sibling discount", () => {
  const pct = { siblingMode: "Percentage" as const, secondChild: 10, thirdAndLater: 15 };
  it("applies from the second child", () => {
    expect(siblingPays(pct, 1, 150000)).toBe(150000);
    expect(siblingPays(pct, 2, 150000)).toBe(135000);
    expect(siblingPays(pct, 3, 150000)).toBe(127500);
    expect(siblingPays({ siblingMode: "FixedAmount", secondChild: 20000, thirdAndLater: 200000 }, 3, 150000)).toBe(0);
    expect(siblingPays({ ...pct, siblingMode: "None" }, 2, 150000)).toBe(150000);
  });

  it("validates like the API", () => {
    expect(siblingErrors({ siblingMode: "Percentage", secondChild: 120, thirdAndLater: -1 })).toEqual({ secondChild: "At most 100%.", thirdAndLater: "Enter 0 or more." });
    expect(siblingErrors({ siblingMode: "FixedAmount", secondChild: 120000, thirdAndLater: 1.234 })).toEqual({ thirdAndLater: "Use at most 2 decimal places." });
    expect(siblingErrors({ siblingMode: "None", secondChild: -5, thirdAndLater: 0 })).toEqual({});
  });
});

describe("instalments", () => {
  it("needs 2–3 parts adding to 100% with dates in order", () => {
    const ok = [
      { percent: "60", dueOn: "2026-09-15" },
      { percent: "40", dueOn: "2026-10-15" },
    ];
    expect(instalmentErrors(ok)).toEqual({ rows: {}, total: null });
    expect(instalmentTotal(ok)).toBe(100);
    const bad = instalmentErrors([
      { percent: "50", dueOn: "2026-10-15" },
      { percent: "40", dueOn: "2026-10-15" },
      { percent: "", dueOn: "" },
    ]);
    expect(bad.rows).toEqual({ 1: "Must be due after the previous part.", 2: "Enter a percentage above 0." });
    expect(bad.total).toBe("Parts add up to 90%; they must add up to 100%.");
    expect(instalmentErrors([{ percent: "100", dueOn: "2026-10-15" }]).total).toBe("Use 2 or 3 parts.");
    expect(instalmentErrors([{ percent: "33.33", dueOn: "2026-09-01" }, { percent: "33.33", dueOn: "2026-10-01" }, { percent: "33.34", dueOn: "2026-11-01" }]).total).toBeNull();
  });
});

describe("generate toast", () => {
  it("summarises the run", () => {
    expect(generateToast({ created: 120, alreadyInvoiced: 4, noFees: 2, totalBilled: 18_400_000 })).toBe("Created 120 · 4 already invoiced · 2 with no fees · ₦18.4M billed");
    expect(generateToast({ created: 0, alreadyInvoiced: 0, noFees: 0, totalBilled: 0 })).toBe("Created 0 · ₦0 billed");
  });
});
