import type { ProofStatus } from "@/lib/api/types";
import { formatNaira, parseAmount } from "./rules";

/** Proof of bank payment (E10.8): client checks mirroring the API (which re-checks and re-encodes photos). */

export const PROOF_ACCEPT = "image/jpeg,image/png,image/webp,application/pdf";
export const MAX_PROOF_PHOTO_BYTES = 12 * 1024 * 1024;
export const MAX_PROOF_PDF_BYTES = 5 * 1024 * 1024;

export function proofFileError(file: { type: string; size: number; name: string } | null): string | null {
  if (!file) return "Add a photo or PDF of your transfer receipt.";
  if (/\.(heic|heif)$/i.test(file.name) || /heic|heif/i.test(file.type)) return "iPhone HEIC photos aren't accepted. Share it as JPEG (choose “Most Compatible”) or take a screenshot.";
  if (file.size === 0) return "That file is empty.";
  if (file.type === "application/pdf") return file.size > MAX_PROOF_PDF_BYTES ? "PDFs must be 5 MB or smaller." : null;
  if (["image/jpeg", "image/png", "image/webp"].includes(file.type)) return file.size > MAX_PROOF_PHOTO_BYTES ? "Photos must be 12 MB or smaller." : null;
  return "Upload a photo (JPEG, PNG or WebP) or a PDF.";
}

export type ProofErrors = Partial<Record<"amount" | "paidOn" | "bankReference" | "note", string>>;

export function proofErrors(v: { amount: string; paidOn: string; bankReference: string; note: string }, balance: number, today: string): ProofErrors {
  const e: ProofErrors = {};
  const n = parseAmount(v.amount);
  if (n === null || n <= 0) e.amount = "Enter the amount you paid, above ₦0.";
  else if (n > balance) e.amount = `That's more than the balance of ${formatNaira(balance)}.`;
  if (!v.paidOn) e.paidOn = "Choose the date you paid.";
  else if (v.paidOn > today) e.paidOn = "The payment date can't be in the future.";
  if (v.bankReference.trim().length > 100) e.bankReference = "Use 100 characters or fewer.";
  if (v.note.trim().length > 500) e.note = "Use 500 characters or fewer.";
  return e;
}

export const proofStatusLabel: Record<ProofStatus, string> = { Pending: "Being checked", Confirmed: "Confirmed", Rejected: "Not accepted" };
export const proofStatusTone: Record<ProofStatus, "warning" | "success" | "danger"> = { Pending: "warning", Confirmed: "success", Rejected: "danger" };
