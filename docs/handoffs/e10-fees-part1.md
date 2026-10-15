# Handoff → Frontend: E10 part 1 — Fee items, opt-ins, sibling discount, instalment plans, invoices

> Backend status as of 2026-10-15. Contract: `contracts/openapi.json` (run `npm run api:types`). Payments, receipts, PDFs, statements and the debtors list come in part 2; Paystack in part 3.

## What exists now
- **Fee items per term:** name, amount (₦, 2 dp), one class level or school-wide, mandatory or **optional**, and whether the **sibling discount** applies (typically tuition). `POST /fee-items/copy` copies a term's items, and who opted in, to an empty term.
- **Optional items are per-student opt-in** (user decision): bus, lunch and so on are billed only to students ticked for that item.
- **Sibling discount.** The admin chooses the mode in Settings → Fees.
  - Modes: `None`, `Percentage` (2nd child X%, 3rd and later Y%), or `FixedAmount` (₦X off for the 2nd child, ₦Y for each later child).
  - Families are students linked through any shared guardian. The eldest (earliest date of birth) is child 1 and pays in full.
  - The discount is calculated on discount-eligible items and appears as a negative invoice line, e.g. "Sibling discount (2nd child)".
- **Instalment plans.** The admin sets them for a **term** or for the **whole session**; a term's own plan wins.
  - 2 or 3 parts adding up to 100%, due in date order. Plans can be switched off (`enabled: false`), which means pay in full.
  - Each invoice shows its schedule, rounded to the kobo, with the parts adding up exactly.
- **Invoice generation:**
  - Bills every active student (of one class level, or all) for a term: mandatory items + opted-in optional items − sibling discount.
  - Numbered `INV-2026-2027-0001` per session.
  - **Idempotent**, with one live invoice per student per term (DB-enforced): run it again after new admissions and only new students are billed.
- **Invoices show the student's admission number** (user request) on the list and the detail page, for tracing. Part 2's invoice and receipt PDFs will print it prominently, and it doubles as the bank-transfer reference.
- Changing fee items, the discount or plans affects **invoices generated afterwards** only.

## Endpoints: policy Finance (Admin, Bursar); ✱ = Admin only
| Method & path | Body / query → Response |
|---|---|
| `GET /api/finance/settings` | → `{ siblingMode: "None"\|"Percentage"\|"FixedAmount", secondChild, thirdAndLater }` |
| ✱ `PUT /api/finance/settings` | same shape → same. 400 `secondChild` (percent > 100, negative, > 2 dp) |
| `GET /api/finance/fee-items?termId` | → `FeeItemDto[]` (school-wide first, then by level; mandatory before optional) |
| `POST /api/finance/fee-items` | `{ termId, name, amount, classLevelId?, isMandatory, siblingDiscount }` → `FeeItemDto` · 400 `name` / `amount` / `classLevelId` |
| `PUT /api/finance/fee-items/{id}` | same body → `FeeItemDto` (making it mandatory clears opt-ins) |
| `DELETE /api/finance/fee-items/{id}` | 204 (existing invoices keep their lines) |
| `POST /api/finance/fee-items/copy` | `{ fromTermId, toTermId }` → `FeeItemDto[]` · 422 `fee.term_has_items` |
| `GET /api/finance/fee-items/{id}/opt-ins` | → `OptInStudent[]` (active students of the item's level, or everyone) |
| `PUT /api/finance/fee-items/{id}/opt-ins` | `{ studentIds }` → `OptInStudent[]` · 422 `fee.mandatory` · 400 `studentIds` |
| `GET /api/finance/instalment-plan?termId` | → `InstalmentPlanDto` (`scope`: `None` \| `Term` \| `Session`) |
| ✱ `PUT /api/finance/instalment-plan` | `{ termId, scope: "None"\|"Term"\|"Session", enabled, instalments: [{ percent, dueOn }], clearSession? }` → `InstalmentPlanDto` · 400 `instalments` |
| `POST /api/finance/invoices/generate` | `{ termId, classLevelId? }` → `{ created, alreadyInvoiced, noFees, totalBilled }` |
| `GET /api/finance/invoices?termId&classRoomId&status&search&page&pageSize` | → `PagedResult<InvoiceSummary>` (search: name, admission no., invoice no.) |
| `GET /api/finance/invoices/{id}` | → `InvoiceDetail` (lines, total, paid, balance, status, dueOn, schedule) |

```ts
type FeeItemDto = { id; termId; name; amount: number; classLevelId: string | null; classLevel: string | null; isMandatory: boolean; siblingDiscount: boolean; optedIn: number };
type OptInStudent = { studentId; admissionNumber; name; classRoom: string | null; optedIn: boolean };
type InstalmentPlanDto = { termId; scope: "None" | "Term" | "Session"; enabled: boolean; instalments: { order: number; percent: number; dueOn: string }[] };
type InvoiceSummary = { id; number; studentId; admissionNumber; studentName; classRoom: string | null; termId; term; total; paid; balance;
  status: "Unpaid" | "PartiallyPaid" | "Paid" | "Cancelled"; dueOn: string | null; issuedAt: string };
type InvoiceDetail = InvoiceSummary-ish & { session: string; lines: { description: string; amount: number }[];   // discounts are negative
  schedule: { order: number; percent: number; amount: number; dueOn: string }[] };                               // [] = pay in full by dueOn
```

## UI to build: Finance section (Admin and Bursar; nav "Fee items", "Invoices")
1. **Fee items (`/admin/fees`):**
   - Term picker. A table grouped "All classes" then each level, with name, amount (₦ formatted), Mandatory/Optional badge, "Sibling discount" badge, and opted-in count.
   - Add/edit dialog, delete, and "Copy from another term" (shown when the term is empty).
   - Optional items get a **"Who takes this?"** sheet: a searchable student list with checkboxes grouped by class, plus a select-all per class.
2. **Settings → Fees (admin):**
   - **Sibling discount:** mode radio (None / Percentage / Fixed amount), two inputs labelled per mode ("2nd child", "3rd child and later") with % or ₦, and a live example: "Child 2 pays ₦135,000 of ₦150,000 tuition".
   - **Instalments:** a term picker showing the current scope, plus Off / This term / Whole session.
     - Use 2 or 3 rows of % + due date, with a running total that must reach 100% and dates in order.
     - Bursars see the plan read-only.
3. **Invoices (`/admin/invoices`):**
   - "Generate invoices" (term + optional class level) shows the result toast "Created 120 · 4 already invoiced · 2 with no fees · ₦18.4m billed".
   - The table has filters for term, class, status and search, with columns number, **admission no.**, student, class, total, paid, balance, status badge and due date.
   - The detail page has lines (discounts shown negative, in green) and the instalment schedule.
4. Use money formatting `₦150,000.00` everywhere, with right-aligned tabular numbers.

## Next (part 2)
Record payments (cash, transfer, POS) with receipt numbers; reversals; invoice and receipt PDFs showing the admission number; cancel invoices; per-student statements; debtors list; CSV export; parents' Fees tab in the portal.
