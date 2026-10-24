# Handoff → Frontend: formatted content (editor) for news, notices and events

> Backend status as of 2026-10-24. Contract: `contracts/openapi.json` (run `npm run api:types`).

## What changed
- Posts and events can now carry **formatted HTML** from a rich editor. Tiptap is recommended.
  - `PostInput.bodyHtml` and `EventInput.descriptionHtml` are optional.
  - When present, the server **cleans** the HTML and **derives** the plain `body` / `description` from it, so `body` may be sent as `""`.
  - Sending them as null or empty switches the item back to plain text.
- Responses carry the cleaned HTML:
  - `PostDto.bodyHtml` and `PortalNotice.bodyHtml`;
  - `NewsArticle.bodyHtml` (public);
  - `EventDto.descriptionHtml` and `PublicEvent.descriptionHtml`.

  **Render as HTML when non-null**; otherwise show the plain text, as today.

## Allowed formatting
Anything outside this list is dropped on save:
- **Tags:** `p br h2 h3 h4 strong b em i u s blockquote ul ol li a hr figure figcaption img`.
- **Attributes:** `href src alt title width height class`.
- **Classes:** `button` (a link styled as a button), `align-left` / `align-center` / `align-right`, `wide` (a full-width image).
- **Links:** http, https or mailto only. Every link gets `rel="noopener noreferrer"`.
- **Images:** only from our own store, i.e. a `src` starting `/api/public/site/images/`. Others are stripped, including tracking pixels and hot-linked images.
- **Never kept:** inline styles, scripts, iframes and event handlers.

Limit: 100,000 characters of HTML (400 on the `bodyHtml` / `descriptionHtml` field).

## Pictures inside content
| Method & path | Who | Body | Response |
|---|---|---|---|
| `POST /api/cms/media` | admin | multipart `file` (JPEG, PNG or WebP, up to 12 MB; resized to 1600 px) | `{ id, url }` |

- Put `url` in the image's `src`. It's `/api/public/site/images/media/{id}?v=…`.
- When rendering on the site, map these srcs to your `/api/site/images/...` proxy, as you already do for `imageUrl`.
- Uploaded media is public by its (unguessable) address as soon as it's uploaded.

## UI to build (with the missing CMS admin screens)
1. **Editor toolbar:**
   - text: H2/H3, bold, italic, underline, bullet and numbered lists, quote, link, button-link, divider;
   - image: upload, alt text and caption required, plus an align / wide toggle.
2. **Paste:** keep pasting from Word or Google Docs working. The server strips styles, so the editor needn't.
3. **Screens:**
   - News & notices: list, create/edit with the editor, cover image, audience, publish/unpublish, notice window, carousel and ticker toggles.
   - Events: create/edit with the editor.
   - Gallery: upload, caption, album, order, featured.
   - Messages: inbox with status.
   - Testimonials, calendar and stats.
   - Newsletter subscribers: list and CSV.

   The endpoints exist (`docs/handoffs/e7-website-content.md`).
4. **Site rendering:** use a `prose` style for `bodyHtml`, with `.button` styled as the brand button and `.wide` images at full width.
