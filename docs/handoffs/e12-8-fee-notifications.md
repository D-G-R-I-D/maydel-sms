# Handoff → Frontend: fee notifications (E12.8) and personal notifications

> Backend status as of 2026-10-24. Contract: `contracts/openapi.json` (run `npm run api:types`).

## What families get
- **Every payment** notifies the child's guardians, whether the bursar records it, Paystack confirms it, or a proof of payment is confirmed:
  - a portal notification: "Payment received for Ada", with the amount, the receipt number and the remaining balance;
  - an email with the receipt PDF, sent from `accounts@`. For proof confirmations the existing confirmation email already carries the receipt, so no second email goes out.
- **Overdue fees:** when an instalment (or the due date, if there's no plan) has passed and is unpaid:
  - a portal notification, "Ada's fees are overdue";
  - an email.

  These repeat every *N* days (default 7) until the overdue amount is paid. Reminders only go out between 08:00 and 18:00 WAT, checked hourly. The admin can switch them off or change *N*.
- Only guardians with a portal login get the portal notification. Only guardians with an email get the email.

## Personal notifications (new, reusable)
This is separate from the CMS notices (`/api/notices`, which are posts for an audience). These notifications are **for one user**. Homework and the communication book (E17) will use the same feed.

| Method & path | Who | Response |
|---|---|---|
| `GET /api/notifications?unreadOnly` | any signed-in user | `{ unread, items: [{ id, kind, title, body, linkUrl, studentId, createdAt, isRead }] }`, newest first, ≤ 50 |
| `POST /api/notifications/{id}/read` | the owner | 204; 404 `notification.not_found` for anyone else's |
| `POST /api/notifications/read-all` | any signed-in user | 204 |

`kind`: `PaymentReceived` | `FeesOverdue` (more kinds come with E17). `linkUrl` is a portal path (`/portal/fees`); open it on click and mark the notification read.

## Reminder settings (admin)
| Method & path | Who | Body / response |
|---|---|---|
| `GET /api/finance/reminders` | admin, bursar | `{ enabled, intervalDays }` |
| `PUT /api/finance/reminders` | admin | `{ enabled, intervalDays }` (1–60; 400 field `intervalDays`) |

## UI to build
- **Bell (portal, and later the student and console headers):**
  - The badge is `notices.unread + notifications.unread`.
  - The dropdown lists both: notifications first, with an icon per kind (lucide `Receipt` for payments, `AlertTriangle` for overdue), then notices.
  - Clicking one marks it read and follows `linkUrl`. "Mark all read" calls both read-all endpoints.
- **Fees page:** no change needed. The notification links to `/portal/fees`.
- **Admin → Settings → Fees:** a "Reminders" card with an on/off switch and "Remind every [7] days", plus a one-line explanation (office hours, until paid).

## Demo
- Record a payment for a child whose guardian has a login (`parent@maydel.test`). The parent's bell shows it, and the email appears in the Pickup folder (`backend/src/Maydel.Api/emails`).
- Overdue reminders need a published invoice whose instalment date has passed. On the dev data the worker sends them within an hour during office hours.
