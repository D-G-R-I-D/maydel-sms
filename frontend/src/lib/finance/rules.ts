import type { FeeItem, FeeSettings, GenerateInvoicesResult, InvoiceStatus } from "@/lib/api/types";

/** Fees (E10 part 1): money formatting and the client-side rules the screens need. The API re-checks. */

const naira = new Intl.NumberFormat("en-NG", { style: "currency", currency: "NGN", minimumFractionDigits: 2, maximumFractionDigits: 2 });
/** ₦150,000.00 */
export function formatNaira(amount: number): string {
  return naira.format(amount).replace("NGN", "₦").replace(/\s/g, "");
}

const compact = new Intl.NumberFormat("en-NG", { notation: "compact", maximumFractionDigits: 1 });
/** ₦18.4M, for summaries. */
export function formatNairaShort(amount: number): string {
  return `₦${compact.format(amount)}`;
}

/** Parses "150,000", "₦150000.5", " 150000 " → 150000.5; null when not a non-negative amount with ≤ 2 dp. */
export function parseAmount(input: string): number | null {
  const cleaned = input.replace(/[₦,\s]/g, "");
  if (!/^\d+(\.\d{1,2})?$/.test(cleaned)) return null;
  return Number(cleaned);
}

export type FeeGroup = { key: string; label: string; items: FeeItem[] };

/** "All classes" first, then each level (in the API's order); mandatory before optional inside a group. */
export function groupFeeItems(items: readonly FeeItem[]): FeeGroup[] {
  const groups = new Map<string, FeeGroup>();
  for (const item of items) {
    const key = item.classLevelId ?? "all";
    const g = groups.get(key) ?? { key, label: item.classLevel ?? "All classes", items: [] };
    g.items.push(item);
    groups.set(key, g);
  }
  const list = [...groups.values()];
  list.sort((a, b) => Number(b.key === "all") - Number(a.key === "all"));
  for (const g of list) g.items.sort((a, b) => Number(b.isMandatory) - Number(a.isMandatory));
  return list;
}

export function groupTotal(items: readonly FeeItem[]): number {
  return items.filter((i) => i.isMandatory).reduce((sum, i) => sum + i.amount, 0);
}

/** What the nth child (2 = second) pays on an amount that's eligible for the sibling discount. */
export function siblingPays(settings: Pick<FeeSettings, "siblingMode" | "secondChild" | "thirdAndLater">, child: number, amount: number): number {
  if (child < 2 || settings.siblingMode === "None") return amount;
  const value = child === 2 ? settings.secondChild : settings.thirdAndLater;
  const off = settings.siblingMode === "Percentage" ? (amount * Math.min(Math.max(value, 0), 100)) / 100 : Math.max(value, 0);
  return Math.max(0, Math.round((amount - off) * 100) / 100);
}

export function siblingErrors(s: Pick<FeeSettings, "siblingMode" | "secondChild" | "thirdAndLater">): Partial<Record<"secondChild" | "thirdAndLater", string>> {
  const errors: Partial<Record<"secondChild" | "thirdAndLater", string>> = {};
  if (s.siblingMode === "None") return errors;
  for (const k of ["secondChild", "thirdAndLater"] as const) {
    const v = s[k];
    if (!Number.isFinite(v) || v < 0) errors[k] = "Enter 0 or more.";
    else if (s.siblingMode === "Percentage" && v > 100) errors[k] = "At most 100%.";
    else if (Math.round(v * 100) !== v * 100) errors[k] = "Use at most 2 decimal places.";
  }
  return errors;
}

export type InstalmentRow = { percent: string; dueOn: string };

/** 2–3 parts, each > 0 %, adding up to exactly 100 %, due dates set and strictly in order. */
export function instalmentErrors(rows: readonly InstalmentRow[]): { rows: Record<number, string>; total: string | null } {
  const out: Record<number, string> = {};
  let total = 0;
  rows.forEach((r, i) => {
    const p = Number(r.percent);
    if (r.percent.trim() === "" || !Number.isFinite(p) || p <= 0) out[i] = "Enter a percentage above 0.";
    else if (Math.round(p * 100) !== p * 100) out[i] = "Use at most 2 decimal places.";
    else if (!r.dueOn) out[i] = "Choose a due date.";
    else if (i > 0 && rows[i - 1].dueOn && r.dueOn <= rows[i - 1].dueOn) out[i] = "Must be due after the previous part.";
    if (Number.isFinite(p)) total += p;
  });
  const sum = Math.round(total * 100) / 100;
  const totalError = rows.length < 2 || rows.length > 3 ? "Use 2 or 3 parts." : sum !== 100 ? `Parts add up to ${sum}%; they must add up to 100%.` : null;
  return { rows: out, total: totalError };
}

export function instalmentTotal(rows: readonly InstalmentRow[]): number {
  return Math.round(rows.reduce((s, r) => s + (Number(r.percent) || 0), 0) * 100) / 100;
}

/** "Created 120 · 4 already invoiced · 2 with no fees · ₦18.4M billed" */
export function generateToast(r: GenerateInvoicesResult): string {
  const parts = [`Created ${r.created}`];
  if (r.alreadyInvoiced) parts.push(`${r.alreadyInvoiced} already invoiced`);
  if (r.noFees) parts.push(`${r.noFees} with no fees`);
  parts.push(`${formatNairaShort(r.totalBilled)} billed`);
  return parts.join(" · ");
}

export const invoiceStatusLabel: Record<InvoiceStatus, string> = { Unpaid: "Unpaid", PartiallyPaid: "Part paid", Paid: "Paid", Cancelled: "Cancelled" };
export const invoiceStatusTone: Record<InvoiceStatus, "neutral" | "warning" | "success" | "danger"> = {
  Unpaid: "danger",
  PartiallyPaid: "warning",
  Paid: "success",
  Cancelled: "neutral",
};
