# Maydel SMS — web

Next.js 16 (App Router) + TypeScript strict + Tailwind v4. The browser only ever talks to this app; this app talks to the API (BFF, ADR-002).

```bash
cp .env.example .env.local   # API_BASE_URL=http://localhost:5178
npm ci
npm run dev                  # http://localhost:3000
npm run api:types            # regenerate src/lib/api/schema.d.ts from ../contracts/openapi.json (commit it)
npm run lint && npm run typecheck && npm test
npm run test:e2e             # Playwright smoke; needs the API up (docker compose). E2E_BASE_URL=http://localhost:3000 to reuse a running app
```

Compose: `docker compose --profile web up -d --build` builds `Dockerfile` (standalone output, port 3000).

## How auth works
- `src/app/api/auth/*/route.ts` call the API server-to-server and keep `access_token` / `refresh_token` in **httpOnly, SameSite=Lax** cookies (Secure in production; override with `COOKIE_SECURE`). Only `user` reaches the browser. `maydel_user` caches `MeResponse` (httpOnly) for UX guards.
- `src/proxy.ts` is Next 16's renamed `middleware.ts`: route guards (UX only), the `mustChangePassword` redirect, and silent refresh when the access token has < 2 min left. Refreshes are shared per token for 10 s so parallel requests/prefetches rotate the family once.
- Server Components fetch through `lib/api/server.ts` (`load()`): bearer from cookie, `X-Forwarded-For` + `User-Agent` forwarded. A 401 goes once through `GET /api/auth/refresh?next=…`; a second 401 within 30 s ends at `/login`. A 403 `auth.password_change_required` goes to `/change-password`.
- Client IP is the **right-most** `X-Forwarded-For` hop. In production put a reverse proxy in front that appends the real client address.
- BFF POST routes reject cross-site `Origin`s.

## UI
- Tokens from `docs/05-design-system.md` live in `src/app/globals.css` (light, dark via OS, or the user's choice in the account menu). Fonts are self-hosted from `src/app/fonts` (Inter, Fraunces; OFL).
- Toasts: `sonner` via `components/ui/toast.tsx`. To show one after a redirect, a BFF route sets the `maydel_flash` cookie (`lib/flash.ts`).
- Console: Ctrl/⌘ K command palette. Demo-account quick fill on `/login` in development (`SHOW_DEMO_ACCOUNTS=true` to force it on).

## Notes
- `eslint-config-next` isn't used: its dependency chain pulls `braces`, which has an unfixed high-severity advisory that fails `npm audit --audit-level=high` in CI. typescript-eslint + react-hooks + jsx-a11y cover the rules we need.
- `package-lock.json` is generated with Node 22's npm 10 (the CI/Docker version). If you regenerate it with npm 11, check `npm ci` still passes on Linux.
