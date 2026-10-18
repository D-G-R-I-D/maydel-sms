import { describe, expect, it } from "vitest";
import { proofErrors, proofFileError } from "./proofs";

describe("proof of payment", () => {
  it("accepts photos and PDFs within their limits", () => {
    expect(proofFileError({ type: "image/jpeg", size: 2_000_000, name: "receipt.jpg" })).toBeNull();
    expect(proofFileError({ type: "application/pdf", size: 1_000_000, name: "receipt.pdf" })).toBeNull();
    expect(proofFileError({ type: "application/pdf", size: 6 * 1024 * 1024, name: "big.pdf" })).toBe("PDFs must be 5 MB or smaller.");
    expect(proofFileError({ type: "image/heic", size: 100, name: "IMG_1.HEIC" })).toMatch(/HEIC/);
    expect(proofFileError({ type: "text/html", size: 100, name: "x.html" })).toBe("Upload a photo (JPEG, PNG or WebP) or a PDF.");
    expect(proofFileError(null)).toMatch(/Add a photo or PDF/);
  });

  it("checks amount and date like the API", () => {
    const ok = { amount: "50,000", paidOn: "2026-10-17", bankReference: "", note: "" };
    expect(proofErrors(ok, 200_000, "2026-10-17")).toEqual({});
    expect(proofErrors({ ...ok, amount: "200,000.01" }, 200_000, "2026-10-17").amount).toBe("That's more than the balance of ₦200,000.00.");
    expect(proofErrors({ ...ok, paidOn: "2026-10-18" }, 200_000, "2026-10-17").paidOn).toBe("The payment date can't be in the future.");
    expect(proofErrors({ ...ok, amount: "" }, 200_000, "2026-10-17").amount).toMatch(/above ₦0/);
  });
});
