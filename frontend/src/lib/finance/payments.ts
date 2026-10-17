import type { InvoiceDetail, PaymentMethod } from "@/lib/api/types";
import { formatNaira, parseAmount } from "./rules";

/** Payments (E10 part 2a): client mirror of the API's rules, plus schedule status. The API re-checks. */

export const PAYMENT_METHODS: { value: Exclude<PaymentMethod, "Online">; label: string }[] = [
  { value: "Cash", label: "Cash" },
  { value: "BankTransfer", label: "Bank transfer" },
  { value: "Pos", label: "POS" },
];

export const methodLabel: Record<PaymentMethod, string> = { Cash: "Cash", BankTransfer: "Bank transfer", Pos: "POS", Online: "Online" };

/** −₦5,000.00 for negatives; callers format the absolute value. */
export function signed(amount: number, format: (n: number) => string): string {
  return amount < 0 ? `−${format(-amount)}` : format(amount);
}

export type PaymentFormErrors = Partial<Record<"amount" | "paidOn" | "reference", string>>;

export function paymentErrors(
  input: { amount: string; paidOn: string; reference: string },
  invoice: Pick<InvoiceDetail, "balance" | "instalmentsAllowed" | "minimumPayment">,
  today: string,
): PaymentFormErrors {
  const errors: PaymentFormErrors = {};
  const amount = parseAmount(input.amount);
  if (amount === null || amount <= 0) errors.amount = "Enter an amount above ₦0 with at most 2 decimal places.";
  else if (amount > invoice.balance) errors.amount = "That's more than the balance.";
  else if (!invoice.instalmentsAllowed && amount !== invoice.balance) errors.amount = "Full payment only this term: pay the whole balance.";
  // Each payment must at least complete the next instalment (or clear the balance).
  else if (amount < invoice.minimumPayment && amount !== invoice.balance)
    errors.amount = `Pay at least ${formatNaira(invoice.minimumPayment)} to complete the next instalment, or the full balance of ${formatNaira(invoice.balance)}.`;
  if (input.paidOn && input.paidOn > today) errors.paidOn = "The payment date can't be in the future.";
  if (input.reference.trim().length > 100) errors.reference = "Use 100 characters or fewer.";
  return errors;
}

export type ScheduleState = "paid" | "overdue" | "due" | "upcoming";

/**
 * Each instalment's state from what's been paid: parts are covered in order by `paid`; an uncovered
 * part is overdue once its date has passed, "due" on the day itself, otherwise upcoming.
 */
export function scheduleStates(schedule: readonly { amount: number; dueOn: string }[], paid: number, today: string): ScheduleState[] {
  let covered = paid;
  return schedule.map((s) => {
    const done = covered + 0.005 >= s.amount;
    covered = Math.max(0, covered - s.amount);
    if (done) return "paid";
    if (s.dueOn < today) return "overdue";
    if (s.dueOn === today) return "due";
    return "upcoming";
  });
}

/**
 * The earliest date something is still owed across invoices: the first instalment not yet covered
 * by payments, or the invoice's due date when there's no schedule. Null when nothing is owed.
 */
export function nextDueDate(invoices: readonly Pick<InvoiceDetail, "balance" | "paid" | "dueOn" | "schedule" | "status">[], today: string): string | null {
  const dates: string[] = [];
  for (const inv of invoices) {
    if (inv.status === "Cancelled" || inv.balance <= 0) continue;
    if (inv.schedule.length === 0) {
      if (inv.dueOn) dates.push(inv.dueOn);
      continue;
    }
    const states = scheduleStates(inv.schedule, inv.paid, today);
    const i = states.findIndex((s) => s !== "paid");
    if (i >= 0) dates.push(inv.schedule[i].dueOn);
  }
  return dates.sort()[0] ?? null;
}
