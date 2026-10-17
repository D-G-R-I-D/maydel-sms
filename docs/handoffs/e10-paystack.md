# Handoff → Frontend: E10.5 — Pay online with Paystack (parents' portal)

> Backend status as of 2026-10-17. Contract: `contracts/openapi.json` (run `npm run api:types`). Builds on fees parts 1–2b.

## How it works
1. In the portal Fees tab the parent presses **Pay online** on an invoice. They choose the amount: the **balance** (default) or a part payment that is at least `minimumPayment` (the same instalment rule as at the bursar's desk).
2. `POST /api/portal/children/{studentId}/invoices/{invoiceId}/pay { amount? }` returns `{ reference, authorizationUrl, amount }`. **Redirect the browser to `authorizationUrl`** (Paystack's hosted checkout); no card details ever touch our app.
3. Paystack sends the parent back to `Paystack:CallbackUrl` (default `http://localhost:3000/portal/fees/paid`) with `?reference=…&trxref=…`.
4. The return page calls `GET /api/portal/payments/{reference}`, which confirms with Paystack if the webhook hasn't already. Show the outcome:
   - `Succeeded`: "Payment received", with the receipt number and a download link (`/api/portal/children/{studentId}/receipts/{paymentId}/pdf`).
   - `Pending`: "Waiting for Paystack to confirm…". Poll every 3 s for up to ~30 s, then say "We'll update your balance as soon as Paystack confirms".
   - `Failed`: show the message, with a "Try again" button.
   - `NeedsReview`: "We received your payment but need to check it. The school will contact you." Show `message`.
5. Meanwhile, Paystack also calls `POST /api/webhooks/paystack`, signed with HMAC-SHA512.
   - Forged or unsigned calls get 401.
   - Whichever arrives first, the webhook or the return page, records the payment **exactly once** (DB-enforced); replays are ignored.

## Safety rules
- **Every payment is re-verified with Paystack's API** before it's recorded: status `success`, currency NGN, and exactly the amount we requested.
  - A different amount is **not applied**; it becomes `NeedsReview`.
- If the invoice was settled another way meanwhile, only what's still owed is applied. The rest is flagged as `excess` (`NeedsReview`) for the bursar to refund or reallocate.
- Online payments get normal receipt numbers (`RCT-…`) with method `Online`, and appear on invoices, receipts, statements and the debtors list.
- Other families' children or references → 404. Online payment not configured → 422 `payment.online_unavailable`.

## Endpoints
| Method & path | Who | Response / errors |
|---|---|---|
| `POST /api/portal/children/{studentId}/invoices/{invoiceId}/pay` `{ amount?: number }` | Parent, Student | `OnlineCheckout { reference, authorizationUrl, amount }` · 400 `errors.amount` (below `minimumPayment` / above the balance) · 404 · 422 `payment.online_unavailable` / `invoice.nothing_owed` |
| `GET /api/portal/payments/{reference}` | Parent, Student (own child) | `OnlinePaymentResult { reference, status, amount, invoiceId, studentId, receiptNumber, paymentId, excess, message }` · 404 `payment.online_not_found` |
| `GET /api/finance/online-payments?status&page&pageSize` | Admin, Bursar | `PagedResult<OnlinePaymentRow>`: use `status=NeedsReview` for the "Needs review" list |
| `POST /api/webhooks/paystack` | Paystack only (signed) | 200 / 401: not for the UI |

`status`: `Pending | Succeeded | Failed | NeedsReview`.

## UI to build
1. **Portal Fees:** a **Pay online** button on each invoice with a balance. It opens a sheet:
   - Amount radio: "Full balance NGN …" or "Next instalment: at least NGN `minimumPayment`" (editable, validated client-side).
   - Then POST and `window.location.assign(authorizationUrl)`.
   - Hide or disable the button with a hint when the API returns 422 `payment.online_unavailable`.
2. **`/portal/fees/paid`:** the return page as described above. Read `reference` from the query, and invalidate the fees data afterwards.
3. **Staff:**
   - Add a **"Online payments"** tab on Finance with a status filter and a "Needs review" badge count.
   - Columns: reference, invoice, admission no., student, amount, excess, status, message, started and completed times.
   - Online payments already show in the invoice payments table with method "Online (Paystack)".

## Setup (for the user)
1. **Set the keys:** `Paystack__SecretKey` (environment only, never committed) and `Paystack__PublicKey` (not secret). Optionally `Paystack__CallbackUrl` (the public URL of `/portal/fees/paid`).
2. **Register the webhook:** in the Paystack dashboard → Settings → API Keys & Webhooks, set the URL to `https://<your-domain>/api/webhooks/paystack`. Locally, Paystack can't reach the webhook, so the return page does the confirming.
3. **Test mode:** use test keys (`sk_test_…`) and Paystack's test cards.
