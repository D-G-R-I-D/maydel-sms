# Handoff → Frontend: Phase 0 — Foundation (E0, E1.1–E1.4, E1.6, E14.1)

> Paste this whole file into the frontend Claude session, working from `maydel-sms/`. Backend status as of 2026-10-12.

## What exists now (backend)
- **API** (.NET 10) with sign-in (password + Google), rotating sessions, a first-login password change, role policies, row-level access rules, and an automatic append-only audit log with an admin viewer.
- **Contract:** `contracts/openapi.json` (OpenAPI 3.1, regenerated on every backend build, committed). Generate your types from it; don't hand-write DTOs.
- **Local stack:** `cp .env.example .env && docker compose up -d --build` → API on `http://localhost:5178` (Scalar docs at `/scalar`), Postgres on 5432, demo data seeded.
- **Compose `web` service** is already declared under the `web` profile: build context `./frontend`, port 3000, env `API_BASE_URL=http://api:8080`, `NEXT_PUBLIC_GOOGLE_CLIENT_ID`. Add a `frontend/Dockerfile` that listens on 3000, then run `docker compose --profile web up -d --build`.
- **CI** (`.github/workflows/ci.yml`) has a `frontend` job that switches on as soon as `frontend/package.json` exists. It runs `npm ci`, `npm run api:types` (then fails if `src/lib/api/schema.d.ts` differs from the committed copy), `lint`, `typecheck`, `test`, and `npm audit --audit-level=high`. **Provide exactly those npm scripts.**

## Demo accounts (password `Demo#Pass2026` for all)
| Sign in as | Role | Notes |
|---|---|---|
| `admin@maydel.test` | Admin | sees Audit log |
| `teacher@maydel.test` | Teacher | form teacher of Year 3 A |
| `bursar@maydel.test` | Bursar | |
| `parent@maydel.test` | Parent | 2 children (Eze) |
| `newteacher@maydel.test` | Teacher | `mustChangePassword: true` → exercises first-login flow |
| `MSS/2026/0001` … `MSS/2026/0005` | Student | username = admission number (contains `/`) |

## Endpoints (all JSON, camelCase, enums as strings)
Base: `API_BASE_URL` (server-side only). The browser never calls the API directly; there's no CORS.

| Method & path | Auth | Body → 200 response | Error statuses |
|---|---|---|---|
| `POST /api/auth/login` | anon, 10/min/IP | `{ userName, password }` → `AuthResponse` | 400, 401, 429 |
| `POST /api/auth/google` | anon, 10/min/IP | `{ idToken }` (Google Identity Services credential) → `AuthResponse` | 400, 401, 404 (Google disabled), 429 |
| `POST /api/auth/refresh` | anon, 10/min/IP | `{ refreshToken }` → `AuthResponse` (new pair; old refresh token dies) | 400, 401, 429 |
| `POST /api/auth/logout` | anon | `{ refreshToken }` → **204** (always, even for unknown tokens) | 400, 429 |
| `GET /api/auth/me` | bearer | → `MeResponse` | 401 |
| `POST /api/auth/change-password` | bearer, 10/min/IP | `{ currentPassword, newPassword }` → `AuthResponse` (**replace both cookies**) | 400, 401, 429 |
| `GET /api/audit?entityType&entityId&userId&action&from&to&page&pageSize` | Admin | → `PagedResult<AuditLogEntry>` (newest first; `to` exclusive; pageSize ≤ 200, default 25) | 400, 401, 403 |
| `GET /api/audit/filters` | Admin | → `{ entityTypes: string[], actions: ["Added","Modified","Deleted"] }` | 401, 403 |
| `GET /health` · `GET /health/ready` | anon | liveness · DB readiness | |

```ts
type AuthResponse = {
  accessToken: string; accessTokenExpiresAt: string;   // ISO-8601 UTC, ~15 min
  refreshToken: string; refreshTokenExpiresAt: string; // ~14 days
  user: MeResponse;
};
type MeResponse = {
  id: string; userName: string; fullName: string; email: string | null;
  roles: ("Admin" | "Teacher" | "Bursar" | "Parent" | "Student")[]; // sorted; a user can hold several
  mustChangePassword: boolean;
};
type PagedResult<T> = { items: T[]; page: number; pageSize: number; totalCount: number };
type AuditLogEntry = {
  id: number; timestamp: string; userId: string | null; userName: string | null;
  action: "Added" | "Modified" | "Deleted"; entityType: string; entityId: string | null; ipAddress: string | null;
  changes: Record<string, unknown> | null; // Added/Deleted: { Prop: value }; Modified: { Prop: { from, to } }; secrets are "***"
};
```

## Errors: one shape everywhere (RFC 9457, `application/problem+json`)
```ts
type Problem = { status: number; title: string; detail?: string; code?: string; traceId: string;
                 errors?: Record<string, string[]> }; // errors only on 400, keyed by camelCase field name
```
- **400** `code: "validation"`: show `errors[field]` under each form field (RHF `setError`), keyed by camelCase field (`userName`, `password`, `currentPassword`, `newPassword`).
- **401** with no body code = missing/expired/revoked access token → try one refresh, then send to `/login`.
- Show `detail` to users (it's written for them). Show `traceId` small on error screens ("Quote this to support").

| `code` | Status | UI behaviour |
|---|---|---|
| `auth.invalid_credentials` | 401 | Show `detail` on the form (same text for unknown user / wrong password / locked / disabled: don't try to tell them apart) |
| `auth.session_expired` | 401 | Clear cookies → `/login?next=…` |
| `auth.google_invalid` · `auth.google_email_unverified` · `auth.google_no_account` · `auth.google_mismatch` | 401 | Show `detail` on the login page |
| `auth.google_disabled` | 404 | Hide the Google button (also hide it when `NEXT_PUBLIC_GOOGLE_CLIENT_ID` is empty) |
| `auth.password_change_required` | 403 | Redirect to `/change-password` (any non-auth API call returns this while `mustChangePassword`) |
| (none) 403 | 403 | "You don't have access to this page" state |
| (none) 429 | 429 | "Too many attempts, wait a minute" |

## BFF rules (ADR-002, ADR-005): non-negotiable
1. Route handlers `app/api/auth/{login,google,refresh,logout,change-password}/route.ts` call the API server-to-server, then set **httpOnly; Secure (prod); SameSite=Lax; Path=/** cookies: `access_token` (expires at `accessTokenExpiresAt`) and `refresh_token` (expires at `refreshTokenExpiresAt`). Return only `user` (MeResponse) to the browser. **Tokens never reach browser JS, localStorage or the response body.**
2. When calling the API, forward the browser's IP as **`X-Forwarded-For`** and its **`User-Agent`**. The API rate-limits and audits by that IP (it trusts the header only from the compose network / configured proxies).
3. **Silent refresh** in `middleware.ts` when the access token has < 2 min left, or after a 401 without a `code`. **De-duplicate**: one refresh per request at most. The API tolerates the same refresh token being reused within 20 s (concurrent requests), but after that, reuse = theft and the whole session is revoked.
4. Logout: call `POST /api/auth/logout` with the refresh cookie, then clear both cookies, even if the call fails.
5. After `change-password`, replace both cookies with the returned pair. Every other device is signed out by the API.
6. Route guards in middleware are UX only. Use roles from `MeResponse` (cache it in an httpOnly cookie or re-fetch `/api/auth/me` server-side). `mustChangePassword` → force `/change-password` before any other page.
7. Google: render the Google Identity Services button client-side with `NEXT_PUBLIC_GOOGLE_CLIENT_ID`, POST the `credential` to the BFF route, and the BFF forwards it to `/api/auth/google`. Students can't use Google. The API never creates accounts.

## Password rules (mirror in Zod)
≥ 10 chars, ≤ 128, at least one upper-case letter, one lower-case letter and one digit, different from the current password. The server re-checks and returns field errors.

## Build in this phase (frontend)
1. **Scaffold** `frontend/`: Next.js App Router, TS strict, Tailwind with tokens from `docs/05-design-system.md`, lucide-react, React Hook Form + Zod, Vitest, Playwright, openapi-typescript (`npm run api:types` → `src/lib/api/schema.d.ts` from `../contracts/openapi.json`), plus `Dockerfile`.
2. **`lib/api`** server-only client: typed from `schema.d.ts`, adds the bearer from the cookie, forwards IP/UA, parses `Problem`.
3. **`/login`** (design system: "Unified sign-in (password + Google)"): username field labelled "Email or admission number", password, Google button, error states above. Works at 360 px, keyboard, light/dark.
4. **`/change-password`**: forced for `mustChangePassword`, also reachable from the user menu.
5. **`middleware.ts`**: protects `(console)/admin/*` (Admin/Teacher/Bursar) and `(portal)/portal/*` (Parent/Student), handles silent refresh and the password-change redirect. After sign-in, staff land on `/admin` and parents/students on `/portal`.
6. **Console + portal shells**: layout, role-filtered sidebar (sections from design system §IA; only Dashboard + Audit log have pages now; the rest can be placeholders hidden behind role), user menu with sign-out.
7. **`/admin/audit`** (Admin): filter bar (entity type + action from `/api/audit/filters`, entity id, date range), paged table (timestamp, user, action, entity, IP), and a row expander rendering `changes` as a before/after diff with `***` shown as "redacted". Include loading, empty and error states.
8. **Playwright smoke** (E0.2): sign in as admin → see dashboard shell → open audit log; sign in as `newteacher@maydel.test` → forced to change password.

## Not in this phase
Self-service password reset (E1.5), MFA (E1.7), any school data screens (students, classes, scores…): those APIs arrive in Phase 1 with their own handoffs.
