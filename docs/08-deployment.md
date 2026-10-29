# Deployment runbook (going live)

> Written 2026-10-29. Target: **Railway** for the app and database, **Cloudflare R2** for uploaded files, **Resend** for email. One API instance, one web instance, one Postgres. Everything here is settings and clicks; no code changes are needed.

## 1. What runs where

| Piece | Where | Built from | Public address |
|---|---|---|---|
| Website + portals (Next.js) | Railway service `web` | `frontend/Dockerfile` | `https://maydelspringschools.com` (and `www.`) |
| API (.NET) | Railway service `api` | `backend/Dockerfile` | `https://api.maydelspringschools.com` |
| Database | Railway **Postgres** (17) | — | private only |
| Uploaded files (photos, documents, hand-ins) | Cloudflare **R2** bucket `maydel-files` | — | private (served through the API) |
| Email | **Resend**, domain `maydelspringschools.com` | — | — |

**Why the API gets its own address.** Newsletter pictures and one-click unsubscribe links must work from inside people's inboxes (`Email__PublicApiUrl`). The website itself calls the API over Railway's **private network**.

## 2. Accounts to create (the school owns them)
1. **Railway:** one project, "Maydel".
2. **Cloudflare:** an R2 bucket plus an API token with "Object Read & Write" on that bucket only. The domain's DNS can live here too.
3. **Resend:** add the domain, then add the DNS records it shows (SPF, DKIM, and DMARC `v=DMARC1; p=quarantine`). Create an API key with "Sending access" only.
4. **Google Cloud** (sign in with Google for families): an OAuth client of type "Web application".
   - Authorised JavaScript origins: `https://maydelspringschools.com` and `https://www.maydelspringschools.com`.
5. **Paystack** (when ready): live keys, with the webhook URL set to `https://api.maydelspringschools.com/api/webhooks/paystack`.
6. **AI question helper** (optional): a Gemini API key (free tier), and/or an Anthropic key later.

Keep every key in Railway's variables only. Never put one in the code, in chat, or in a document.

## 3. API settings (`api` service variables)
| Variable | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Default` | from Railway Postgres: `Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}};SSL Mode=Prefer` |
| `Database__MigrateOnStartup` | `true` (one API instance: schema updates apply on start; **back up first**, see §6) |
| `Jwt__SigningKey` | a random 64-character secret: `openssl rand -base64 48` |
| `Seed__AdminEmail` / `Seed__AdminPassword` | the first admin login, used **once**. Remove the password variable after the first sign-in and password change |
| `Seed__DemoData` | `false` (the default; production starts empty) |
| `Seed__StarterContent` | `true` (the default; starter website pages and gallery, added once) |
| `Storage__Provider` | `S3` |
| `Storage__ServiceUrl` | `https://<account-id>.r2.cloudflarestorage.com` |
| `Storage__Bucket` / `Storage__Region` | `maydel-files` / `auto` |
| `Storage__AccessKeyId` / `Storage__SecretAccessKey` | the R2 token's keys |
| `Storage__ForcePathStyle` | `true` |
| `Email__Provider` | `Resend` |
| `Email__ResendApiKey` | the Resend key |
| `Email__FromAddress` / `Email__FromName` | `no-reply@maydelspringschools.com` / `Maydel Spring Schools` |
| `Email__AdmissionsAddress` · `Email__AccountsAddress` · `Email__ResultsAddress` | `admissions@` · `accounts@` · `results@` (all on the domain; verified in Resend) |
| `Email__ReplyTo` | the office inbox people should reply to |
| `Email__PortalUrl` | `https://maydelspringschools.com` |
| `Email__PublicApiUrl` | `https://api.maydelspringschools.com` |
| `Email__DailyLimit` | `100` on Resend's free plan; `0` once on a paid plan |
| `Authentication__Google__ClientId` | the Google OAuth client id |
| `ForwardedHeaders__KnownNetworks__0` | `10.0.0.0/8`, so Railway's proxy is trusted for real client IPs (rate limits) |
| `Paystack__SecretKey` / `Paystack__PublicKey` | live keys (when ready) |
| `Paystack__CallbackUrl` | `https://maydelspringschools.com/portal/fees/paid` |
| `Paystack__AdmissionsCallbackUrl` | `https://maydelspringschools.com/admissions/paid` |
| `Ai__GeminiApiKey` (optional) | enables the AI question helper for free; `Ai__ApiKey` (Anthropic) takes over when set |
| `School__*` | not needed: the school profile is edited in Settings and starts from the defaults (name, motto, address, phone, email) |

Health checks: `GET /health` (alive) and `GET /health/ready` (database reachable). Set Railway's health check path to `/health/ready`.

## 4. Website settings (`web` service variables)
| Variable | Value |
|---|---|
| `API_BASE_URL` | the API's **private** address, e.g. `http://api.railway.internal:8080` |
| `SITE_URL` / `PUBLIC_SITE_URL` | `https://maydelspringschools.com` |
| `TRUSTED_ORIGINS` | `https://maydelspringschools.com,https://www.maydelspringschools.com` |
| `COOKIE_SECURE` | `true` |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | the same Google client id (needed at **build** time) |
| `SHOW_DEMO_ACCOUNTS` | `false` |

## 5. Domain (DNS)
| Record | Name | Points to |
|---|---|---|
| CNAME | `@` / `www` | the `web` service's Railway domain (add both custom domains in Railway first) |
| CNAME | `api` | the `api` service's Railway domain |
| TXT/MX/CNAME | as shown by Resend | SPF, DKIM, return path |
| TXT | `_dmarc` | `v=DMARC1; p=quarantine; rua=mailto:admin@maydelspringschools.com` |

Railway issues the HTTPS certificates automatically once the records resolve.

## 6. Backups
- **Database:** turn on Railway's Postgres backups (daily). Before every deploy that changes the database (a PR with a migration), take a manual backup in Railway first.
- **Files:** R2 is durable. For a second copy, enable R2's object versioning, or run a monthly copy to another bucket.
- **Test a restore** once before launch: restore the backup into a scratch database and sign in.

## 7. Launch-day checklist
1. Deploy `api` and wait for `/health/ready` to report healthy (the migrations run on this first start).
2. Deploy `web` and open the site; check the home, news and admissions pages.
3. Sign in with the seeded admin. Change the password, then remove `Seed__AdminPassword` from the variables.
4. **Settings:**
   - School profile: upload the logo and check the address and phone.
   - Sessions & terms: create the current session and set the current term.
   - Classes, subjects, fees, store prices.
5. Create staff accounts (teachers, bursar, drivers), then students and guardians.
6. Send yourself a **test newsletter** and a **test fee receipt**. Check that the pictures load and the links open.
7. Issue the **clock-in kiosk link** and open it on the reception tablet.
8. Point the old site's DNS to the new site (keep the old one at `legacy.` for 30 days, per the roadmap).
9. Make the GitHub repository **private** again if it was opened.

## 8. Costs to expect (approximate)
- **Railway:** usage-based. A small school deployment (API + web + Postgres) typically runs about US$10–20 a month.
- **Cloudflare R2:** free up to 10 GB stored; no download fees.
- **Resend:** free for 3,000 emails a month (100 a day), or about US$20 a month for 50,000.
- **Domain:** yearly renewal at the registrar.
