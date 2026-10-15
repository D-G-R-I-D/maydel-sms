# Handoff → Frontend: E10 part 2a — Payments, receipts, reversals, cancel, adjustments, statements, debtors

> Backend status as of 2026-10-15. Contract: `contracts/openapi.json` (run `npm run api:types`). Builds on part 1 (`e10-fees-part1.md`). Invoice/receipt PDFs and the parents' portal Fees tab are part 2b.

## Rules
- **Recording a payment** (Admin, Bursar):
  - Methods: cash, bank transfer or POS. `Online` is refused, because it's reserved for Paystack.
  - Amounts: > 0, at most 2 dp, never more than the balance.
  - **Without an enabled instalment plan, the full balance must be paid** (400 `errors.amount`). With one, any part payment is fine.
  - `paidOn` defaults to today (WAT) and can't be in the future.
  - Each payment gets a receipt **`RCT-2026-2027-00001`**, numbered per session.
- **Receipts are never edited.**
  - A mistake is **reversed by an admin**: the reversal is a new negative entry with its own number, and needs a reason.
  - Each payment can be reversed once (422 `payment.already_reversed`). A reversal can't itself be reversed.
- **Cancel an invoice** (admin, reason required): only when no money is on it (422 `invoice.has_payments`; reverse payments first). The student can then be invoiced again by **Generate**.
- **Adjustments** (admin): e.g. a scholarship or one-off discount (negative), or an extra charge (positive). The total can't drop below what's already paid (400 `errors.amount`).
- **Overdue:**
  - `dueNow` is what should have been paid by today: the instalments due so far, or the whole total once `dueOn` has passed when there's no plan.
  - `overdue = max(0, dueNow − paid)`.

## Endpoints: Finance policy (Admin, Bursar); ✱ = Admin only
| Method & path | Body / query → Response | Errors |
|---|---|---|
| `POST /api/finance/invoices/{id}/payments` | `{ amount, method: "Cash"\|"BankTransfer"\|"Pos", reference?, paidOn? }` → `ReceiptDto` | 400 `amount` / `method` / `paidOn`; 404; 422 `invoice.cancelled` |
| ✱ `POST /api/finance/payments/{id}/reverse` | `{ reason }` → `ReceiptDto` (negative amount) | 400 `reason`; 404; 422 `payment.already_reversed` / `payment.is_reversal` |
| ✱ `POST /api/finance/invoices/{id}/cancel` | `{ reason }` → `InvoiceDetail` | 400 `reason`; 422 `invoice.has_payments` / `invoice.cancelled` |
| ✱ `POST /api/finance/invoices/{id}/adjustments` | `{ description, amount }` → `InvoiceDetail` | 400 `description` / `amount` |
| `GET /api/finance/students/{id}/statement` | → `StudentStatement` | 404 |
| `GET /api/finance/debtors?termId&classRoomId?&overdueOnly?` | → `DebtorsReport` (most overdue first) | 404 |
| `GET /api/finance/debtors.csv?…same` | `text/csv` (UTF-8 BOM, opens in Excel; formula-like cells neutralised) | |

`InvoiceDetail` gains: `payments: PaymentDto[]`, `instalmentsAllowed`, `dueNow`, `overdue`, `cancelReason`.

```ts
type PaymentDto = { id; receiptNumber; amount: number /* negative for reversals */; method; reference: string | null; paidOn: string;
  isReversal: boolean; reversesReceipt: string | null; reversedBy: string | null; note: string | null; recordedAt: string };
type ReceiptDto = { paymentId; receiptNumber; amount; method; reference; paidOn; isReversal; invoiceId; invoiceNumber;
  admissionNumber; studentName; classRoom: string | null; term: string; balanceAfter: number };
type StudentStatement = { studentId; admissionNumber; name; classRoom; totalBilled; totalPaid; balance;
  entries: { date: string; kind: "Invoice" | "Payment" | "Reversal"; reference: string; description: string; debit: number; credit: number; balance: number }[] };
type DebtorsReport = { termId; term; count; totalOutstanding; totalOverdue;
  rows: { studentId; admissionNumber; name; classRoom; invoiceId; invoiceNumber; total; paid; balance; dueNow; overdue;
          nextDueOn: string | null; guardian: string | null; guardianPhone: string | null }[] };
```

## UI to build
1. **Invoice detail:**
   - **Record payment** button (bursar/admin) opens a dialog: amount prefilled with the balance, method, reference and date. When `instalmentsAllowed` is false, explain "Full payment only this term" and lock the amount to the balance.
   - After saving, show the receipt panel: receipt number, **admission number**, student, amount, method and balance after. Add "Print" once PDFs land in part 2b.
   - A payments table: reversals in red with a "Reverses RCT-…" note, and reversed payments struck through ("Reversed by RCT-…").
   - Admin-only actions:
     - **Reverse** a payment (asks for a reason).
     - **Add adjustment** (description + amount, with a "Scholarship / discount" or "Extra charge" toggle that sets the sign).
     - **Cancel invoice** (asks for a reason; disabled while there's money on the invoice).
   - Show the instalment schedule with Due now / Overdue badges.
2. **Debtors (`/admin/debtors`):**
   - Term and class filters, an "Overdue only" toggle, and summary cards for total outstanding and total overdue.
   - Table: admission no., student, class, balance, overdue (red when > 0), next due, guardian, and phone as a `tel:` link.
   - **Export CSV** downloads through the BFF.
3. **Student statement:** a "Fees" tab on the student page listing entries with the running balance, and totals.
4. Money is always shown as ₦, right-aligned with tabular numbers. Show negatives as −₦.

## Update 2026-10-15: instalment minimums (user decision)
- With an enabled plan, **each payment must at least complete the next instalment**.
  - 60/40: the first payment is ≥ 60%, and the next is ≥ the remaining 40%.
  - 50/30/20: payments must reach the 50% → 80% → 100% marks.
  - Paying more than an instalment, or the full balance, is always fine.
- Below the minimum → 400 `errors.amount` ("Pay at least ₦X to complete the next instalment, or the full balance of ₦Y.").
- `InvoiceDetail.minimumPayment`: the least the next payment can be. It's the balance when there's no plan, and 0 when nothing is owed. The Record payment dialog should show it and validate against it.
