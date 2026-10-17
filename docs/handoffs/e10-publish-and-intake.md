# Handoff → Frontend: E10.6 + E10.7 — New vs returning fee items, optional on/off by default, draft → publish invoices

> Backend status as of 2026-10-17. Contract: `contracts/openapi.json` (run `npm run api:types`).

## What changed
- **Fee item audience** (`audience`): `All` (default), `NewIntake` (acceptance fee, uniform pack, books…) or `Returning` (re-registration…).
  - A student is **new intake** for a term when admitted during it or up to 120 days before it starts (summer admissions).
  - Invoices record `newIntake`, and the list can filter by it.
- **Optional items have two modes** (`chargedByDefault`):
  - `false`, **off by default**: only students ticked pay it (bus). As before.
  - `true`, **on by default**: everyone in the item's level pays it unless unticked (books, lunch).
  - **When `chargedByDefault` is left out, it is on for New-intake items** (uniform pack, books) so nothing a new student needs is forgotten, and off otherwise. Preselect it the same way in the dialog.
  - `GET/PUT /fee-items/{id}/opt-ins` always talks about **who pays**. `optedIn: true` means they pay, and PUT `studentIds` = everyone who pays, in both modes.
  - `FeeItemDto.optedIn` counts takers of an off-by-default item; `optedOut` counts exclusions from an on-by-default item.
  - Switching mode, or making an item mandatory, clears the ticks.
- **Invoices start as Drafts.** `Generate` creates drafts that are staff-only: parents can't see them, download them or pay them online.
  - `POST /api/finance/invoices/publish { termId, classLevelId?, notifyParents? }` publishes the drafts and, by default, emails each guardian with an address the **invoice PDF**, the total, the due date, "quote admission number …" and the portal link.
  - Running it again only publishes (and emails about) new drafts, never twice.
  - Invoices that existed before this change were migrated as **already published**.
- Cancelled or unpublished invoices are never emailed. The PDF is attached at send time, only while the invoice is still live and published.

## API changes
| Change | Detail |
|---|---|
| `POST/PUT /api/finance/fee-items` body | + `audience?: "All" \| "NewIntake" \| "Returning"`, + `chargedByDefault?: boolean` |
| `FeeItemDto` | + `audience`, `chargedByDefault`, `optedOut` |
| `POST /api/finance/invoices/publish` | `{ termId, classLevelId?, notifyParents? }` → `{ published, alreadyPublished, emailsQueued }` (Admin, Bursar) |
| `GET /api/finance/invoices` | + `published?: boolean`, `newIntake?: boolean` filters; `InvoiceSummary` + `isPublished`, `newIntake` |
| `InvoiceDetail` | + `isPublished`, `publishedAt`, `newIntake` |
| Portal fees, PDFs, pay online | only **published** invoices (404 otherwise) |

## UI to build
1. **Fee item dialog:**
   - An "Applies to" select: All students / New intake only / Returning only.
   - For optional items, a "Charged by default" switch with the hint "On: everyone pays unless unticked (books, lunch). Off: only ticked students pay (bus)."
   - Badges in the fee table: *New intake*, *Returning*, *On by default*. The opt-in sheet is titled "Who pays this?" and the counts read "12 paying" or "3 excluded".
2. **Invoices page:**
   - **Draft** and **Published** badges, a Draft/Published filter and a "New intake" filter, plus a "New intake" tag in the table.
   - A **Publish invoices** button (term + optional class level) with an "Email parents a copy" checkbox, ticked by default. The confirm dialog says "Publish N draft invoices? Parents of these students will be emailed the invoice."
   - Result toast: "Published N · M already published · E emails queued".
   - After **Generate**, nudge with "N invoices are drafts. Review them, then Publish."
3. On the invoice detail page, show a "Draft: not visible to parents yet" banner until it's published.
