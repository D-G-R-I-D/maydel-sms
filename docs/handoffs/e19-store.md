# Handoff → Frontend: store and parent shop (E19)

> Backend status as of 2026-10-25. Contract: `contracts/openapi.json` (run `npm run api:types`).

## What it does
- **Catalogue.**
  - Items (`Uniform`, `Sportswear`, `Books`, `IdCard`, `Other`) have **options** (sizes, house colours), each with its own price, stock and low-stock level.
  - An item can be limited to sections: `Creche`, `Nursery`, `Primary`, `JuniorSecondary`, `SeniorSecondary`. With none, it's for everyone.
  - An item goes **on sale** only once every active option has a price.
- **Starter list.** Created once and **not on sale** until the admin sets prices: blazer, white shirt, tie, skirt, trousers (secondary), shorts (primary), house sportswear and replacement ID card. Sizes and houses are placeholders to rename.
- **Stock.**
  - **Restock** records quantity, unit cost and supplier. **Adjust** adds or removes stock with a required reason.
  - Every change is logged: restock, sale, adjustment, reserve (a parent order holds stock) and release.
  - The **low-stock** list shows options at or below their level.
  - Two people selling the last item at the same moment: the second gets **409** instead of overselling.
- **Office sale.** Bursar or admin sells to a student by cash, bank transfer, POS, or **added to the fees** (a line on the student's invoice for the current term; 422 `store.no_invoice` if there's none). It's handed over at once (`handedOver`, default true).
- **Parent shop** (portal):
  - A parent sees only the items for **their child's section** and orders by "Pay at the office" or "Add to fees". The fees option exists only if the admin allows it; it's on by default.
  - The order **holds the stock**. Unpaid orders are released after the hold time (default **48 h**, checked every 15 minutes).
  - The office marks it **Ready**, and the parent gets a `StoreOrderReady` notification (link `/portal/shop/orders/{id}`).
  - An order can't be **Collected** until paid or charged to fees.
  - A parent can cancel while it's still `Placed`.
- **Later:** online card payment for shop orders (needs Paystack keys), printed receipts, and ID-card printing.

## Store endpoints (`/api/store`, admin and bursar; settings PUT is admin only)
| Method & path | Body | Response / errors |
|---|---|---|
| `GET /items?category&includeInactive` | | `StoreItemDto[]` |
| `POST /items` · `PUT /items/{id}` | `{ name, category, description?, sections, isActive, variants: [{ id?, label, price, lowStockAt, isActive }] }` | `StoreItemDto`. Options left out of a PUT are retired, not deleted. 400 `isActive` = unpriced |
| `PUT /items/{id}/image` · `DELETE /items/{id}/image` | multipart `file` | `StoreItemDto` |
| `POST /variants/{id}/restock` | `{ quantity, unitCost?, supplier?, note? }` | `VariantDto` |
| `POST /variants/{id}/adjust` | `{ change, reason }` | `VariantDto`; 422 `store.negative_stock` |
| `GET /variants/{id}/movements` | | `MovementDto[]` (newest first) |
| `GET /low-stock` | | `[{ itemId, item, variantId, variant, stock, lowStockAt }]` |
| `GET /orders?status&studentId&page` | | `PagedResult<StoreOrderDto>` |
| `POST /orders` | `{ studentId, lines: [{ variantId, quantity }], payment: Cash \| BankTransfer \| Pos \| Fees, reference?, handedOver? }` | `StoreOrderDto`; 422 `store.out_of_stock` / `store.no_invoice`; 409 on a stock race |
| `POST /orders/{id}/pay` | `{ method: Cash \| BankTransfer \| Pos, reference? }` | `StoreOrderDto` |
| `POST /orders/{id}/ready` · `/collected` · `/cancel` | cancel: `{ reason? }` | `StoreOrderDto`; 422 `order.unpaid` / `order.state` / `order.paid_cancel` |
| `GET /settings` · `PUT /settings` | `{ parentsCanChargeToFees, holdHours }` | `StoreSettingsDto` |

- `StoreItemDto = { id, name, category, description, sections, isActive, imageUrl, variants: [{ id, label, price, stock, lowStockAt, isLow, isActive }] }`.
- `StoreOrderDto = { id, number (ORD-2026-00001), studentId, student, admissionNumber, fromPortal, placedBy, status: Placed | Ready | Collected | Cancelled, payment: Unpaid | Paid | ChargedToFees, paidWith, paymentReference, total, lines, createdAt, paidAt, readyAt, collectedAt, cancelledAt, cancelReason, holdUntil, invoiceNumber }`.

## Parent shop (`/api/portal/children/{studentId}/shop`, parents)
| Method & path | Body | Response / errors |
|---|---|---|
| `GET ` | | `ShopCatalogue { studentId, student, canChargeToFees, holdHours, items: StoreItemDto[] }` (on-sale items for the child's section) |
| `GET /orders` | | `StoreOrderDto[]` |
| `POST /orders` | `{ lines: [{ variantId, quantity }], payment: AtOffice \| Fees }` | `StoreOrderDto`; 422 `store.out_of_stock` / `shop.no_fees` / `store.no_invoice`; 403 `shop.parents_only` |
| `POST /orders/{orderId}/cancel` | | `StoreOrderDto`; 422 `order.state` |

## UI to build
- **Admin/bursar, Store:**
  - **Catalogue:** grouped by category, with item cards (photo, sections, on-sale toggle).
  - **Item editor:** an options table (label, price, low-stock level, stock) with Restock / Adjust per row and a movements drawer.
  - **Low stock:** a widget.
  - **Sell:** pick a student, add items, choose payment, print or show the order.
  - **Orders:** a queue with status tabs (New / Ready / Collected / Cancelled); actions Ready → Take payment → Collected; Cancel.
  - **Settings:** fees on/off, hold hours.
- **Parent portal, Shop:**
  - a child picker and product cards with option, size and colour selects, showing "out of stock";
  - a cart, then the payment choice (Pay at the office / Add to fees), then a confirmation with the order number and "We'll tell you when it's ready";
  - "My orders" with a status timeline and cancel while `Placed`.
- Add `StoreOrderReady` (ShoppingBag icon) to the bell.

## Printing (added 2026-10-26)
| Method & path | Who | Response |
|---|---|---|
| `GET /api/store/orders/{id}/receipt.pdf` | admin, bursar | A5 receipt; "UNPAID" / "CANCELLED" stamp when so |
| `GET /api/portal/children/{studentId}/shop/orders/{orderId}/receipt.pdf` | the child's parents | the same receipt (404 for anyone else) |
| `GET /api/students/{id}/id-card.pdf` | admin | one ID card: page 1 the front, page 2 the back (85.6 × 54 mm pages) |
| `GET /api/academics/classrooms/{id}/id-cards.pdf` | admin | every active student in the class, 10 per A4 sheet with cut lines; each sheet of fronts is followed by its sheet of backs |

- **ID cards (redesigned 2026-10-29)** have the school crest as a faint watermark on both sides.
  - **Front:** navy header with the crest and school name, the photo in a gold frame (the student's initials when there is no photo), name, admission number, class and session, and a footer with the motto and "valid until" the session's end date.
  - **Back:** the school's name, address, phone and email; "If found, please return to the school or call …"; the admission number in large type; and the principal's signature line (the uploaded signature image and principal's name from Settings, when set).
  - There are no parent details, in case a card is lost.
- **Double-sided printing (class sheets):** the backs page has its columns swapped left-to-right, so printing "two-sided, flip on long edge" at **actual size (100%)** puts each back behind its own front. Each page says so in small grey text at the top. Tell admins this next to the "Print class ID cards" button.
- Not included: a QR or barcode (no library for it yet). Ask if the school wants one.
- **422 `idcard.no_session`** means no current session is set.

**UI:**
- "Receipt" buttons on the store order view and in the parent's "My orders".
- "Print ID card" on the student profile, and "Print class ID cards" on the class page.

## Single lookups (added 2026-10-29)
| Method & path | Who | Response |
|---|---|---|
| `GET /api/store/items/{id}` | admin, bursar | `StoreItemDto` (404 `store.item_not_found`) |
| `GET /api/portal/shop/orders/{orderId}` | any parent or student linked to the order's child | `StoreOrderDto` (404 `store.order_not_found` for anyone else) |

Use the second one for the `StoreOrderReady` notification link (`/portal/shop/orders/{id}`), which carries no student id.
