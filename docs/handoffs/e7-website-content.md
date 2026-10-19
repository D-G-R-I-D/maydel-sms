# Handoff → Frontend: E7 website content — news, notices, carousel, ticker, events, gallery, staff, contact (E7.3, E7.9, E7.10)

> Backend status as of 2026-10-19. Contract: `contracts/openapi.json` (run `npm run api:types`). The public site's pages read these anonymous endpoints; admins manage the content in the console.

## How it works
- **Posts** cover news and notices in one model.
  - A post is a **draft** until published.
  - Audience: `Public`, `Parents`, `Students`, `Staff` (flags; `Everyone`). Only `Public` posts reach the website.
  - On publish a post gets a fixed **slug** (`/news/{slug}`). It keeps the slug when edited, and a title clash gets `-2`.
  - Optional **picture** (JPEG ≤ 1600 px, public URL once published).
  - Optional **notice window**: `showFrom` / `showUntil`.
  - `isFeatured` makes it a **pop-up**.
  - `linkUrl` + `linkText` add a call to action: `https://…` or a site path like `/admissions/apply`. The link text defaults to "Learn more".
  - `inTicker` puts it in the news ticker; `inCarousel` puts it on the homepage carousel, which needs a picture.
- **Notices envelope (E7.10):** `GET /api/public/site/notices` returns current public notices (posts with a window or the pop-up flag, inside the window), featured first.
  - Store "read" ids per browser (`localStorage`) and badge the unread count.
  - A featured notice pops up once per visitor **per `version`**. The version changes when the post is edited, so an edit shows it again.
  - Never block the page; Esc or ✕ closes it.
- **Homepage (E7.9):** `GET /api/public/site/highlights` returns `slides` and `ticker`.
  - `slides` holds up to 10: featured news with pictures, then featured gallery photos, each with `href`.
  - `ticker` holds up to 10 items `{ text, href }`.
  - The carousel auto-advances about every 6 s right-to-left. Pause it on hover, focus or touch; support swipe and arrow keys; show dots; **no autoplay with `prefers-reduced-motion`**; keep a fixed aspect ratio (no layout shift).
- **Events:** staff create events with an audience. Public ones come back upcoming-first from `/events`.
- **Gallery:** photos in albums (`category`) with an order. `isFeatured` puts a photo on the carousel.
- **Staff page:** admin chooses who appears, with job title, bio (≤ 2000) and order. The photo is their staff photo, served publicly only for staff on the site.
- **Contact form:** rate-limited, with a hidden `website` honeypot (send it empty). Messages go to the console inbox and are emailed to the school address (Settings → School). Statuses: `New`, `Read`, `Replied`, `Archived`.
- **Images:** all public pictures use `/api/public/site/images/{news|gallery|staff}/{id}?v=…`, publicly cached for 7 days (the URL changes when the picture does). Unpublished or private items return 404.

## Endpoints — public (anonymous)
| Method & path | Response |
|---|---|
| `GET /api/public/site/news?category&page&pageSize` | `PagedResult<NewsSummary { id, slug, title, summary, category, isPinned, publishedAt, imageUrl }>`, pinned first |
| `GET /api/public/site/news/{slug}` | `NewsArticle { …, body, updatedAt, linkUrl, linkText }`; 404 |
| `GET /api/public/site/notices` | `PublicNotice[] { id, title, summary, body, linkUrl, linkText, isFeatured, publishedAt, version }` |
| `GET /api/public/site/highlights` | `{ slides: { kind: "News"\|"Photo", id, title, caption, imageUrl, href }[], ticker: { id, text, href }[] }` |
| `GET /api/public/site/events?take` | `PublicEvent[] { id, title, description, startsAt, endsAt, location }` |
| `GET /api/public/site/gallery?category` | `{ categories: string[], photos: { id, title, caption, category, imageUrl }[] }` |
| `GET /api/public/site/staff` | `PublicStaff[] { id, name, jobTitle, bio, photoUrl }` |
| `POST /api/public/site/contact` | `{ name, email, phone?, subject?, message, website: "" }` → 204; 400 (`name`, `email`, `message`, `phone`, `subject`); 429 |
| `GET /api/public/site/images/{kind}/{id}` | image; 404 |

## Endpoints — CMS (Admin)
| Method & path | Body / response |
|---|---|
| `GET /api/cms/posts?search&published&page&pageSize` | `PagedResult<PostDto>` (drafts included, newest first) |
| `GET/PUT /api/cms/posts/{id}`, `POST /api/cms/posts` | `PostInput { title, summary?, body, category?, audience, isPinned, showFrom?, showUntil?, isFeatured, linkUrl?, linkText?, inTicker, inCarousel }` → `PostDto`; 400 fields `title`, `body`, `audience`, `showUntil`, `linkUrl` |
| `POST /api/cms/posts/{id}/publish` · `/unpublish` · `DELETE /api/cms/posts/{id}` | `PostDto` / 204 |
| `PUT /api/cms/posts/{id}/image` (multipart `file`) · `DELETE …/image` | `PostDto`; removing the picture of a carousel post → 400 `file` |
| `GET /api/cms/events?from` · `POST` · `PUT/DELETE /api/cms/events/{id}` | `EventInput { title, description?, startsAt, endsAt?, location?, audience }` → `EventDto`; 400 `title` / `endsAt` |
| `GET /api/cms/gallery?category` · `POST` (multipart `file`, `title`, `caption?`, `category`, `order?`, `isFeatured?`) · `PUT/DELETE /api/cms/gallery/{id}` | `GalleryPhotoDto` |
| `GET /api/cms/messages?status&page&pageSize` · `PUT /api/cms/messages/{id}/status { status }` | `ContactMessageDto` |
| `PUT /api/cms/staff/{staffId}` | `{ showOnWebsite, jobTitle?, bio?, websiteOrder }` → `PublicStaff` |

## UI to build
1. **Website (www):** home (carousel, ticker, latest news, upcoming events, envelope), news list and article (SEO metadata from title/summary/image), events, gallery (album filter, lightbox), staff, and contact (form + map + WhatsApp link).
2. **Console → Website (Admin):**
   - Posts: list with draft/published filter; editor with live preview, picture, notice window, pop-up, link, ticker and carousel toggles; publish/unpublish.
   - Events, Gallery (multi-upload, album, order, featured), Staff on website, and a **Messages** inbox with status.
