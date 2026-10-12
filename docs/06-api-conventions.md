# 06 — API Conventions

- Base path `/api/{module}`; resources plural nouns: `/api/students/{id}/guardians`.
- JSON camelCase; enums as strings; dates `YYYY-MM-DD` (`DateOnly`), instants ISO-8601 UTC.
- Money: decimal numbers in NGN with 2 dp (never floats in the frontend — format with `Intl.NumberFormat('en-NG', { style: 'currency', currency: 'NGN' })`).
- Commands that are not CRUD use verbs as sub-resources: `POST /api/assessment/reports/publish`, `POST /api/academics/terms/{id}/make-current`.
- Lists: `?page=1&pageSize=25&search=` → `{ items, page, pageSize, totalCount }`; `pageSize` max 200.
- Errors: RFC 9457 problem details.
  | Status | When |
  |---|---|
  | 400 | Validation (`errors` dictionary keyed by field) |
  | 401 | Not signed in / token invalid |
  | 403 | Signed in but not allowed (role or row-level) |
  | 404 | Not found (also used when existence itself is sensitive) |
  | 409 | Uniqueness / concurrency conflict |
  | 422 | Business rule violated (`detail` is user-presentable) |
  | 429 | Rate limited |
- Idempotency: payment webhooks keyed by gateway reference; invoice generation keyed by (student, term).
- Versioning: additive changes only; breaking changes → `/api/v2/...` (not expected for v1).
- OpenAPI at `/openapi/v1.json` (dev), Scalar UI at `/scalar`; the frontend generates types from it (`npm run api:types`).
- Every endpoint declares its authorization policy explicitly; anonymous endpoints are only under `/api/public`, `/api/auth`, `/api/files` (public folders) and payment webhooks.
