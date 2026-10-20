# Handoff → Frontend: portal notices (E12.1) and department senders (E12.7)

> Backend status as of 2026-10-20. Contract: `contracts/openapi.json` (run `npm run api:types`).

## Portal notices (E12.1)
- These are the same posts as the website CMS (`/api/cms/posts`). The post's **audience** decides who sees it:
  - `Parents`: parent accounts;
  - `Students`: student accounts;
  - `Staff`: admin, teachers and bursars;
  - `Public`: the website and applicants.
  - `Everyone` covers all of them.
- Only **published** posts inside their `showFrom` / `showUntil` window appear.
- Each signed-in user has their own read state. The badge shows `unread`.

| Method & path | Who | Response |
|---|---|---|
| `GET /api/notices?unreadOnly` | any signed-in user | `{ unread, notices: [{ id, title, summary, body, category, imageUrl, linkUrl, linkText, isPinned, isFeatured, publishedAt, isRead }] }`, pinned then newest, ≤ 50 |
| `POST /api/notices/{id}/read` | any signed-in user | 204; 404 if it isn't a notice for them |
| `POST /api/notices/read-all` | any signed-in user | 204 |

**UI:**
- A bell or envelope in the portal, console and student headers, with the unread badge. Poll on navigation or every few minutes.
- A dropdown or page listing notices. Opening one marks it read, and there's a "Mark all read" action.
- `isFeatured` notices can show once as a banner after sign-in.
- In the CMS post editor, the audience picker now matters for portal users too: offer Website / Parents / Students / Staff checkboxes.

## Department senders (E12.7)
- Emails now come from the department:
  - **admissions@** for every admissions stage and offer letter;
  - **accounts@** for invoices, receipts and proof decisions;
  - **results@** for report cards;
  - everything else from the default address.
- Server settings: `Email:AdmissionsAddress`, `Email:AccountsAddress`, `Email:ResultsAddress`. Any left empty falls back to `Email:FromAddress`. Each must be a verified sender with the email provider.
- Contact-form emails to the school now set **Reply-To** to the visitor, so the office just presses Reply.
- No UI change.

## Fix
- The website stats were hidden (`shown: false`) on existing installs because the new switch defaulted to off. They now default to on, and existing schools are switched on.
