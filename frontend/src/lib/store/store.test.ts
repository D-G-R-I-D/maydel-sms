import { describe, expect, it } from "vitest";
import { addToCart, cartTotal, formatSections, parseSections, sectionsLabel, unpricedOptions, type CartLine } from "./store";

const line = (variantId: string, quantity: number, stock = 5, price = 1000): CartLine => ({ variantId, item: "Shirt", variant: "Age 6", price, quantity, stock });

describe("store sections", () => {
  it("round-trips the server's flag string", () => {
    expect(parseSections("Primary, JuniorSecondary")).toEqual(["Primary", "JuniorSecondary"]);
    expect(formatSections(["JuniorSecondary", "Primary"])).toBe("Primary, JuniorSecondary");
  });
  it("treats none and all as everyone", () => {
    expect(formatSections([])).toBe("None");
    expect(sectionsLabel("None")).toBe("Everyone");
    expect(sectionsLabel("All")).toBe("Everyone");
    expect(sectionsLabel("Nursery")).toBe("Nursery");
  });
});

describe("store pricing", () => {
  it("counts only active options without a price", () => {
    expect(unpricedOptions([{ isActive: true, price: 0 }, { isActive: false, price: 0 }, { isActive: true, price: 4500 }])).toBe(1);
  });
});

describe("cart", () => {
  it("merges the same option and caps at stock", () => {
    const one = addToCart([], line("a", 1, 3));
    const more = addToCart(addToCart(one, line("a", 1, 3)), line("a", 5, 3));
    expect(more).toHaveLength(1);
    expect(more[0].quantity).toBe(3);
  });
  it("totals price × quantity", () => {
    expect(cartTotal([line("a", 2, 5, 4500), line("b", 1, 5, 5000)])).toBe(14000);
  });
});
