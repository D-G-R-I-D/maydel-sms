import { NextResponse, type NextRequest } from "next/server";
import {
  ACCESS_COOKIE, REFRESH_COOKIE, USER_COOKIE, clearSession, decodeUser, encodeUser, writeSession, type SessionUser,
} from "@/lib/auth/cookies";
import { forwardingHeaders } from "@/lib/auth/forwarding";
import { refreshSession, sessionIsDead } from "@/lib/auth/refresh";
import { canEnter, homeFor } from "@/lib/auth/roles";
import { jwtExpiry, needsRefresh } from "@/lib/auth/token";
import { publicUrl } from "@/lib/http/redirect";

const PATH_HEADER = "x-maydel-path";

function isProtected(pathname: string): boolean {
  return (
    pathname === "/admin" || pathname.startsWith("/admin/") ||
    pathname === "/portal" || pathname.startsWith("/portal/") ||
    pathname === "/change-password"
  );
}

/**
 * Session upkeep and UX route guards (the API remains the security boundary):
 * - silent refresh when the access token has < 2 min left, at most once per request;
 * - signed-out users on protected pages → /login?next=…;
 * - mustChangePassword → /change-password before anything else;
 * - console for staff, portal for parents/students.
 */
export async function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  const guarded = isProtected(pathname) || pathname === "/login" || pathname === "/";

  let access = request.cookies.get(ACCESS_COOKIE)?.value;
  const refreshToken = request.cookies.get(REFRESH_COOKIE)?.value;
  let user: SessionUser | null = decodeUser(request.cookies.get(USER_COOKIE)?.value);
  let refreshed: Awaited<ReturnType<typeof refreshSession>> | null = null;
  let killSession = false;

  if (guarded && refreshToken && (needsRefresh(access) || !user)) {
    refreshed = await refreshSession(refreshToken, forwardingHeaders(request.headers));
    if (refreshed.ok) {
      access = refreshed.auth.accessToken;
      user = refreshed.auth.user;
    } else if (sessionIsDead(refreshed.problem)) {
      killSession = true;
      access = undefined;
      user = null;
    }
  } else if (guarded && !refreshToken && access && !user) {
    access = undefined;
  }

  const accessExpiry = access ? jwtExpiry(access) : null;
  const signedIn = user !== null && accessExpiry !== null && accessExpiry > Date.now();

  let target: string | null = null;
  if (!signedIn) {
    if (isProtected(pathname)) target = `/login?next=${encodeURIComponent(pathname + search)}`;
    else if (pathname === "/") target = "/login";
  } else if (user) {
    if (user.mustChangePassword) {
      if (guarded && pathname !== "/change-password") target = "/change-password";
    } else if (pathname === "/login" || pathname === "/") {
      target = homeFor(user.roles);
    } else if (!canEnter(pathname, user.roles)) {
      target = homeFor(user.roles);
    }
  }

  let response: NextResponse;
  if (target) {
    response = NextResponse.redirect(publicUrl(request, target));
  } else {
    // Hand the fresh token to this request's Server Components, not just the browser.
    if (refreshed?.ok) {
      request.cookies.set(ACCESS_COOKIE, refreshed.auth.accessToken);
      request.cookies.set(REFRESH_COOKIE, refreshed.auth.refreshToken);
      request.cookies.set(USER_COOKIE, encodeUser(refreshed.auth.user));
    } else if (killSession) {
      request.cookies.delete(ACCESS_COOKIE);
      request.cookies.delete(REFRESH_COOKIE);
      request.cookies.delete(USER_COOKIE);
    }
    const forwarded = new Headers(request.headers);
    forwarded.set(PATH_HEADER, pathname + search);
    response = NextResponse.next({ request: { headers: forwarded } });
  }

  if (refreshed?.ok) writeSession(response.cookies, refreshed.auth);
  else if (killSession) clearSession(response.cookies);
  return response;
}

export const config = {
  matcher: ["/((?!api/|_next/static|_next/image|favicon\\.ico|brand/|.*\\.(?:svg|png|jpg|jpeg|webp|avif|ico|txt|xml)$).*)"],
};
