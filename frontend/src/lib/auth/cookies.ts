import type { AuthResponse, MeResponse } from "@/lib/api/types";
import { FLASH_COOKIE, encodeFlash, type Flash } from "@/lib/flash";

export const ACCESS_COOKIE = "access_token";
export const REFRESH_COOKIE = "refresh_token";
/** Cached MeResponse for UX route guards and the shell. httpOnly; never trusted for authorisation. */
export const USER_COOKIE = "maydel_user";
/** Short-lived marker: a 401-triggered refresh already happened, so a second 401 goes to /login. */
export const REFRESHED_COOKIE = "maydel_refreshed";

type CookieOptions = {
  httpOnly?: boolean;
  secure?: boolean;
  sameSite?: "lax" | "strict" | "none";
  path?: string;
  expires?: Date;
  maxAge?: number;
};

/** The subset of ResponseCookies / cookies() that we need. */
export interface CookieWriter {
  set(name: string, value: string, options?: CookieOptions): unknown;
}

function secureFlag(): boolean {
  const explicit = process.env.COOKIE_SECURE;
  if (explicit !== undefined) return explicit === "true";
  return process.env.NODE_ENV === "production";
}

export function baseCookieOptions(): CookieOptions {
  return { httpOnly: true, secure: secureFlag(), sameSite: "lax", path: "/" };
}

export type SessionUser = Pick<MeResponse, "id" | "userName" | "fullName" | "email" | "roles" | "mustChangePassword">;

export function encodeUser(user: MeResponse): string {
  const slim: SessionUser = {
    id: user.id,
    userName: user.userName,
    fullName: user.fullName,
    email: user.email,
    roles: user.roles,
    mustChangePassword: user.mustChangePassword,
  };
  return Buffer.from(JSON.stringify(slim), "utf8").toString("base64url");
}

export function decodeUser(value: string | undefined): SessionUser | null {
  if (!value) return null;
  try {
    const parsed: unknown = JSON.parse(Buffer.from(value, "base64url").toString("utf8"));
    if (
      typeof parsed === "object" && parsed !== null &&
      "id" in parsed && typeof parsed.id === "string" &&
      "userName" in parsed && typeof parsed.userName === "string" &&
      "fullName" in parsed && typeof parsed.fullName === "string" &&
      "roles" in parsed && Array.isArray(parsed.roles) &&
      "mustChangePassword" in parsed && typeof parsed.mustChangePassword === "boolean"
    ) {
      const email = "email" in parsed && typeof parsed.email === "string" ? parsed.email : null;
      return {
        id: parsed.id,
        userName: parsed.userName,
        fullName: parsed.fullName,
        email,
        roles: parsed.roles.filter((r): r is string => typeof r === "string"),
        mustChangePassword: parsed.mustChangePassword,
      };
    }
    return null;
  } catch {
    return null;
  }
}

/** Stores a new token pair + user. Called after login, Google, refresh and change-password. */
export function writeSession(jar: CookieWriter, auth: AuthResponse): void {
  const base = baseCookieOptions();
  const refreshExpires = new Date(auth.refreshTokenExpiresAt);
  jar.set(ACCESS_COOKIE, auth.accessToken, { ...base, expires: new Date(auth.accessTokenExpiresAt) });
  jar.set(REFRESH_COOKIE, auth.refreshToken, { ...base, expires: refreshExpires });
  jar.set(USER_COOKIE, encodeUser(auth.user), { ...base, expires: refreshExpires });
}

export function clearSession(jar: CookieWriter): void {
  const base = baseCookieOptions();
  for (const name of [ACCESS_COOKIE, REFRESH_COOKIE, USER_COOKIE, REFRESHED_COOKIE]) {
    jar.set(name, "", { ...base, maxAge: 0 });
  }
}

export function markRefreshed(jar: CookieWriter): void {
  jar.set(REFRESHED_COOKIE, "1", { ...baseCookieOptions(), maxAge: 30 });
}

/** Leaves a toast for the next page (see lib/flash.ts). Readable by JS on purpose: it holds no secrets. */
export function setFlash(jar: CookieWriter, flash: Flash): void {
  jar.set(FLASH_COOKIE, encodeFlash(flash), { secure: secureFlag(), sameSite: "lax", path: "/", maxAge: 60 });
}
