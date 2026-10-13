# ADR-006 — Media storage: private S3-compatible bucket (Cloudflare R2), photos re-encoded and served through the API

- **Status:** Accepted (2026-10-13)

## Context
Student and staff photos are personal data about children and staff (NDPA 2023). Railway containers have an ephemeral filesystem, so uploads can't live on the API's disk in production. The options considered:

| Option | Cost at our scale (~1–2k photos, < 1 GB) | Privacy model | Notes |
|---|---|---|---|
| **Cloudflare R2** | Free tier: 10 GB storage, 1M writes and 10M reads a month, **no egress fees** | Private bucket by default | S3 API; same code works with Railway buckets, AWS S3, MinIO |
| Cloudinary | Free tier (25 credits a month) | Public delivery URLs by default; private needs signed/authenticated delivery | Excellent for public, transformed images (CDN, resizing on the fly) |
| Railway volume | Cheap | Private | Tied to one service instance; backups and migrations are manual |
| AWS S3 | Small but non-zero (egress billed) | Private | Fine; R2 is cheaper |

## Decision
1. **`IFileStorage`** abstraction with two implementations: **Local** (dev, default) and **S3** (any S3-compatible service). Production uses a **private Cloudflare R2 bucket**. Switching is configuration only: `Storage__Provider=S3` plus `Storage__S3__ServiceUrl/Bucket/AccessKeyId/SecretAccessKey`.
2. **Photos are never public.** They are streamed by `GET /api/students/{id}/photo` and `GET /api/staff/{id}/photo` after the same access rules as the record (AccessRules). The Next.js BFF proxies them to the browser. Responses are `Cache-Control: private` with an ETag, and clients version URLs with `?v={photoVersion}`.
3. **Every upload is re-encoded** (SkiaSharp, MIT): magic-byte check (JPEG/PNG/WebP only), decode with a 50-megapixel bomb guard, EXIF orientation applied, resized to ≤ 800 px, re-encoded as JPEG. This drops all metadata (**GPS location from phones**) and any non-image payload. 12 MB upload cap.
4. Keys are server-generated (`students/{v7 guid}.jpg`), and stored keys are validated against that shape before any file access.

## Consequences
- + Free at the school's scale, private by default, and no vendor lock-in (plain S3 API). The contract tests run against local disk and a real S3 server (S3Mock).
- + Children's photos can't leak through guessable public URLs or carry home GPS coordinates.
- − Every photo view goes through the API. That's fine for portraits; the API sends small re-encoded JPEGs and the browser caches them.
- **Cloudinary stays an option for the *public* website gallery (E7)**, where public CDN delivery and on-the-fly transforms are the point. That would be a separate public-media store, never used for student photos.
