# Handoff → Frontend: E10.8 — Proof of payment (bank transfer / deposit)

> Backend status as of 2026-10-17. Contract: `contracts/openapi.json` (run `npm run api:types`).

## How it works
1. **Parent uploads proof:** in the portal, a parent who paid by bank transfer or at the bank uploads proof against a **published** invoice.
   - The file is a photo (JPEG/PNG/WebP ≤ 12 MB, re-encoded and stripped) or a PDF (≤ 5 MB, signature-checked) of the teller or transfer.
   - Fields: amount (≤ balance), date paid (not in the future), bank reference and an optional note.
   - At most **3 pending** proofs per invoice.
2. **Nothing is recorded yet.** The proof waits in the bursar's **To verify** queue, oldest first.
3. **Bursar reviews it:**
   - **Confirm** (amount, method and date can be corrected; method defaults to bank transfer) records the payment and issues an `RCT-…` receipt linked to the proof.
     - The parent is emailed the **receipt PDF**.
     - The instalment minimum does **not** apply, because the money is already in the bank. It still can't exceed the balance.
   - **Reject** (reason required) records nothing, and the parent is emailed the reason.
4. **Each proof is reviewed once** (422 `proof.already_reviewed`).
5. **Files are private:** only the family and finance staff can open them. They're always served as a **download** (`Content-Disposition: attachment`), never rendered inline, because they're untrusted uploads.

## Endpoints
| Method & path | Who | Body / response | Errors |
|---|---|---|---|
| `POST /api/portal/children/{studentId}/invoices/{invoiceId}/proofs` | Parent, Student | **multipart**: `file`, `amount`, `paidOn` (yyyy-MM-dd), `bankReference?`, `note?` → `ProofDto` | 400 `file` / `amount` / `paidOn`; 404 (not your child's published invoice); 422 `invoice.nothing_owed` / `proof.too_many_pending` |
| `GET /api/portal/children/{studentId}/proofs` | Parent, Student | `ProofDto[]`, newest first | 404 |
| `GET /api/portal/children/{studentId}/proofs/{proofId}/file` | Parent, Student | file download | 404 |
| `GET /api/finance/proofs?status?&all?&page&pageSize` | Admin, Bursar | `PagedResult<ProofDto>`: the Pending queue by default (oldest first); `status=Confirmed\|Rejected` or `all=true` | |
| `GET /api/finance/proofs/{id}/file` | Admin, Bursar | file download | 404 |
| `POST /api/finance/proofs/{id}/confirm` | Admin, Bursar | `{ amount?, method?: "BankTransfer"\|"Cash"\|"Pos", paidOn? }` → `ProofDto` | 400; 404; 422 `proof.already_reviewed` |
| `POST /api/finance/proofs/{id}/reject` | Admin, Bursar | `{ reason }` → `ProofDto` | 400 `reason`; 422 `proof.already_reviewed` |

```ts
type ProofDto = { id; invoiceId; invoiceNumber; studentId; studentName; admissionNumber; amount; paidOn; bankReference: string | null;
  note: string | null; contentType: string; status: "Pending" | "Confirmed" | "Rejected"; rejectReason: string | null;
  receiptNumber: string | null; paymentId: string | null; submittedAt: string; reviewedAt: string | null; invoiceBalance: number };
```

## UI to build
1. **Portal Fees:**
   - A **"I've paid by bank transfer"** button on each published invoice with a balance. It opens a sheet with file picker / camera, amount (prefilled with the balance), date paid, bank reference and note.
   - Explain: "The school will check your payment and send your receipt."
   - A **Payment proofs** list per child with status badges: Pending (amber), Confirmed (green, with receipt download), Rejected (red, with the reason and "Send again").
2. **Staff Finance → "To verify"** (nav badge = pending count):
   - The queue shows student, admission no., invoice, amount, date paid, bank reference, submitted at, and the invoice balance.
   - A **View proof** button downloads the file (BFF proxy). Images may be previewed via an `<img>` of a blob URL; never embed PDFs inline.
   - **Confirm** (dialog prefilled from the proof, editable) shows the receipt panel after saving. **Reject** asks for a required reason.
3. **Invoice detail:** list proofs for the invoice, and link the confirmed one to its receipt.
