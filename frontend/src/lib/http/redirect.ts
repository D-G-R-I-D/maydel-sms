import { NextResponse, type NextRequest } from "next/server";
import { isTrustedOrigin } from "@/lib/auth/origins";

/**
 * Redirect with a relative `Location` (e.g. `/login?next=%2Fadmin`) from route handlers.
 * `NextResponse.redirect(new URL(path, request.url))` bakes in the Host the server saw, which behind a tunnel or
 * proxy is `localhost:3000`, so the browser would leave the public link. Browsers resolve a relative Location
 * against the URL they are actually on. (The proxy/middleware needs absolute URLs: use {@link publicUrl}.)
 */
export function redirectTo(pathWithQuery: string, status: 303 | 307 = 307): NextResponse {
  assertSameSitePath(pathWithQuery);
  return new NextResponse(null, { status, headers: { Location: pathWithQuery } });
}

/**
 * Absolute URL on the host the browser actually used. Honours `X-Forwarded-Host`/`-Proto` (set by dev tunnels,
 * Railway, Vercel) only when that origin is the request's own host or in TRUSTED_ORIGINS, so a spoofed header
 * can't turn a redirect into an open redirect.
 */
export function publicUrl(request: NextRequest, pathWithQuery: string): URL {
  assertSameSitePath(pathWithQuery);
  const forwardedHost = request.headers.get("x-forwarded-host")?.split(",")[0]?.trim();
  const forwardedProto = request.headers.get("x-forwarded-proto")?.split(",")[0]?.trim() ?? "https";
  if (forwardedHost) {
    const origin = `${forwardedProto}://${forwardedHost}`;
    if (forwardedHost === request.nextUrl.host || isTrustedOrigin(origin)) return new URL(pathWithQuery, origin);
  }
  return new URL(pathWithQuery, request.url);
}

function assertSameSitePath(path: string) {
  if (!path.startsWith("/") || path.startsWith("//")) throw new Error(`Not a same-site path: ${path}`);
}
