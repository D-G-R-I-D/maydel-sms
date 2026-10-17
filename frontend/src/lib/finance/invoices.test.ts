import { describe, expect, it } from "vitest";
import { invoicesHref, parseInvoiceSearch } from "./invoices";

describe("invoice search", () => {
  it("reads only valid filters", () => {
    expect(parseInvoiceSearch({ status: "Bogus", page: "-2", search: "  eze " })).toEqual({ termId: undefined, classRoomId: undefined, status: undefined, search: "eze", published: undefined, newIntake: undefined, page: 1 });
    expect(parseInvoiceSearch({ published: "false", newIntake: "true" })).toMatchObject({ published: false, newIntake: true });
    expect(invoicesHref({ page: 1, published: false, newIntake: true })).toBe("/admin/invoices?published=false&newIntake=true");
    expect(parseInvoiceSearch({ status: "PartiallyPaid", page: "3", termId: "t" })).toMatchObject({ status: "PartiallyPaid", page: 3, termId: "t" });
  });

  it("builds hrefs and resets nothing it isn't told to", () => {
    const s = parseInvoiceSearch({ termId: "t", status: "Unpaid", page: "2" });
    expect(invoicesHref(s)).toBe("/admin/invoices?termId=t&status=Unpaid&page=2");
    expect(invoicesHref(s, { page: 1, status: undefined })).toBe("/admin/invoices?termId=t");
    expect(invoicesHref({ page: 1 })).toBe("/admin/invoices");
  });
});
