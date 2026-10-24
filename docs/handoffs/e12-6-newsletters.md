# Handoff → Frontend: newsletters (E12.6)

> Backend status as of 2026-10-24. Contract: `contracts/openapi.json` (run `npm run api:types`). Builds on the rich editor (`docs/handoffs/e7-rich-content.md`).

## What it does
- **Writing:** the admin writes a newsletter in the same rich editor as posts, with pictures via `POST /api/cms/media`.
- **Audience:** parents (optionally one **section** or one **class**), staff, website subscribers, or any combination.
  - Duplicate addresses are merged.
  - Anyone who unsubscribed is left out.
- **Before sending:** **Test** sends one copy to yourself or any address, marked `[Test]`. **Send now** or **schedule** for any time up to 90 days ahead; a schedule can be cancelled while it waits.
- **Branded:** each copy uses the school's email layout, with the editor's headings, buttons, quotes and images styled for inboxes.
  - The preheader is the grey line inboxes show after the subject.
  - Every copy has an unsubscribe link, plus a one-click unsubscribe header when the server's `Email:PublicApiUrl` is set.
- **Website archive:** with "Publish on website" ticked, a sent newsletter gets a public page.
- **Daily limit:** `Email:DailyLimit`. Resend's free plan allows 100 a day. School notices (fees, results, admissions) always go first; newsletter copies take what's left and the rest wait for the next day. Delivery counts show the progress.
- **Double opt-in (NDPA):** website sign-ups get a "Please confirm your subscription" email and receive nothing until they click it.
  - Existing subscribers were marked confirmed.
  - Unsubscribing stops newsletters only; fee and result emails still arrive.

## Admin endpoints (admin only)
| Method & path | Body | Response / errors |
|---|---|---|
| `GET /api/cms/newsletters?page&pageSize` | | `PagedResult<{ id, title, audience, status, scheduledFor, sentAt, recipients, createdAt, updatedAt }>` |
| `GET /api/cms/newsletters/{id}` | | `NewsletterDto`: `{ …, preheader, bodyHtml, section, classRoomId, publishOnWebsite, delivery: { pending, sent, failed }, webUrl }` |
| `POST /api/cms/newsletters` | `{ title, preheader?, bodyHtml, audience, section?, classRoomId?, publishOnWebsite }` | `NewsletterDto`, created as a draft |
| `PUT /api/cms/newsletters/{id}` | same | 422 `newsletter.not_editable` unless it's a draft |
| `DELETE /api/cms/newsletters/{id}` | | drafts only |
| `GET /api/cms/newsletters/{id}/recipients` | | `{ parents, staff, subscribers, total }`: show this before sending |
| `POST /api/cms/newsletters/{id}/test` | `{ email? }` (default: your own address) | 204 |
| `POST /api/cms/newsletters/{id}/send` | `{ at? }` (omitted or past = now) | `NewsletterDto`. Status becomes `Sent` (copies queued) or `Scheduled`; 422 `newsletter.no_recipients` |
| `POST /api/cms/newsletters/{id}/cancel` | | Scheduled → Draft |

- `audience` is a flags enum, sent as a comma list: `"Parents"`, `"Parents, Staff"` or `"Subscribers"`.
- `section`: `Creche | Nursery | Primary | JuniorSecondary | SeniorSecondary`.
- Field errors: `title` (≤150), `preheader` (≤200), `bodyHtml`, `audience`, `classRoomId`, `at`.

`GET /api/cms/subscribers` now lists everyone (`?active=true` for current ones only), and rows include `confirmed`. The CSV export lists only confirmed, active subscribers.

## Public endpoints (anonymous)
| Method & path | Response |
|---|---|
| `GET /api/public/newsletters` | `[{ slug, title, preheader, sentAt }]`, newest first, ≤ 50 |
| `GET /api/public/newsletters/{slug}` | `{ slug, title, preheader, bodyHtml, sentAt }`; 404 `newsletter.not_found` |
| `POST /api/public/newsletter/unsubscribe?t=…` | 204; 404 `newsletter.link` |
| `POST /api/public/newsletter/confirm?t=…` | 204; 404 `newsletter.link` |

Both POSTs are rate-limited and take the token in the query string.

## Pages to build
1. **Website → Newsletter (admin):**
   - **List:** status pills (Draft / Scheduled / Sent), recipient and delivery counts, "New newsletter".
   - **Editor page:**
     - subject, preheader, the rich editor and a template picker;
     - audience checkboxes, plus a section/class picker when Parents is ticked;
     - "Publish on website";
     - a live recipient count from `/recipients`.
   - **Actions:** Save draft · Send test · Schedule… (WAT date and time) · Send now, with a confirm dialog showing the count.
   - **Templates** are frontend content. Start the editor with ready blocks:
     - *Termly newsletter:* a greeting from the head, a highlights list, an image with caption, dates for the diary, a button to the website.
     - *Event invitation:* title, date and place block, details, an RSVP/Contact button.
     - *Announcement:* a short heading, a paragraph, a button.
   - **Subscribers tab:** the existing list with a "Pending confirmation" badge (`confirmed: false`), and CSV export.
2. **Website pages:**
   - `/newsletters`: the archive list.
   - `/newsletters/[slug]`: render `bodyHtml` with the prose style, mapping image srcs to your proxy.
   - `/newsletter/unsubscribe?t=`: a short page with an **Unsubscribe** button that POSTs. Never unsubscribe on page load, because mail scanners open links.
   - `/newsletter/confirm?t=`: POST on a button press too, then show "You're subscribed".
   - Footer sign-up box: after submitting, say "Check your inbox to confirm", plus a one-line privacy note: "We'll only send the school newsletter. Unsubscribe any time."

## Server settings for production
- `Email:PortalUrl`: the site's address, used for unsubscribe and confirm links.
- `Email:PublicApiUrl`: the API's public address, so pictures load in inboxes and one-click unsubscribe works.
- `Email:DailyLimit`: 100 on Resend's free plan; 0 for no limit.
