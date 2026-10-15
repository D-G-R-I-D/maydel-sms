# Handoff → Frontend: E10 part 2b — Invoice & receipt PDFs, parents' Fees tab (E10.3, E6.3)

> Backend status as of 2026-10-15. Contract: `contracts/openapi.json` (run `npm run api:types`). Builds on parts 1 and 2a.

## What exists now
- **Invoice PDF (A4):**
  - School letterhead (uploaded logo or crest). Invoice no., issued date, due date and status.
  - A student box with name, class, term and **admission number printed large**, plus "Bill to" (primary guardian).
  - Lines (discounts negative); total, paid, balance and "overdue now".
  - Instalment schedule and payments table.
  - A **"How to pay: quote the admission number … as the reference"** note.
  - A pale **PAID** / **CANCELLED** stamp when applicable.
- **Receipt PDF (A5):**
  - Receipt no. and date, student and **admission number**.
  - Amount received with method and reference. Invoice no., invoice total, paid to date and balance after.
  - **Reprints show the figures as at that payment** and never change. Reversals print titled "REVERSAL", with the reason and the receipt they reverse.
- Amounts print as `NGN 150,000.00`: the ₦ glyph isn't in every PDF font.
- **Parents' portal Fees** (E6.3), for Parent or Student:
  - The child's live invoices, newest first, each a full `InvoiceDetail` with payments and schedule, plus totals for `balance` and `overdue`.
  - Cancelled invoices are hidden.
  - Another family's child is 404 `portal.student_not_found`. An invoice or receipt that isn't this child's is 404 `invoice.not_found`.
- **Instalment plans:** a term can opt out of its session's plan. `PUT /api/finance/instalment-plan { termId, scope: "Term", enabled: false, instalments: [] }` makes that term "pay in full" and leaves the session's other terms alone.
  - **Please switch "Pay in full" to this.** `scope: None, clearSession: true` removes the plan for **every** term of the session.
  - Keep `None` + `clearSession` only for an explicit "Remove the session plan" action.

## Endpoints
| Method & path | Who | Response |
|---|---|---|
| `GET /api/finance/invoices/{id}/pdf` | Admin, Bursar | `application/pdf` (`Content-Disposition` like `INV-2026-2027-0001 - Ada Okeke.pdf`) |
| `GET /api/finance/payments/{id}/receipt.pdf` | Admin, Bursar | `application/pdf` |
| `GET /api/portal/children/{studentId}/fees` | Parent, Student | `{ studentId, balance, overdue, invoices: InvoiceDetail[] }` |
| `GET /api/portal/children/{studentId}/invoices/{invoiceId}/pdf` | Parent, Student | `application/pdf` |
| `GET /api/portal/children/{studentId}/receipts/{paymentId}/pdf` | Parent, Student | `application/pdf` |

## UI to build
1. **Staff:**
   - **Download / Print** buttons on the invoice detail page (invoice PDF) and on each payment row and the post-payment receipt panel (receipt PDF).
   - Reuse the PDF Save-As/share/View component from #47 via the BFF media proxy.
2. **Portal Fees tab (`/portal/fees`):** mark it `ready: true` in portalNav.
   - Use the child switcher like Results. Show a summary card: "Balance NGN …", "Overdue NGN …" (red), and the next due date from the schedule.
   - Show invoice cards (term, number, total / paid / balance, status badge), each expandable to lines, schedule and payments.
   - Add "Download invoice" and per-payment "Download receipt" buttons.
   - A "How to pay" note quoting the **admission number**. Bank details come with the school profile later; the Paystack "Pay online" button comes in part 3.
   - Empty state: "No fees have been billed yet."
3. **Portal home:** add a small fees line to each child card ("Balance NGN … · Overdue NGN …") when there's an invoice.
