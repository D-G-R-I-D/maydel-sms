# ADR-002 — Own identity + Backend-for-Frontend session handling

- **Status:** Accepted (2026-10-12)

## Context
The school does not want Firebase or another hosted auth vendor. Users: staff, parents, students (no email). Google sign-in is desirable for staff/parents. Tokens in browser storage are exposed to XSS.

## Decision
1. ASP.NET Core Identity is the user store (passwords, lockout, roles, external logins).
2. API issues short-lived JWT access tokens (15 min) and rotating, hashed refresh tokens (14 days) with reuse detection.
3. Next.js is the **BFF**: its route handlers call `/api/auth/*`, then store both tokens in `httpOnly; Secure; SameSite=Lax` cookies. Browser JS never sees tokens. Server Components call the API with the bearer token.
4. Google: browser obtains a Google ID token; BFF forwards it; API validates it and signs in an **existing** account by verified email. No auto-provisioning.

## Consequences
- + XSS cannot steal sessions; API stays stateless and reusable by a future mobile app (which would use the same token endpoints).
- + No vendor lock-in or per-MAU cost.
- − We own password resets, MFA and account recovery flows (planned in E1).
- − Every API call from the browser goes via Next.js (acceptable; same region).
