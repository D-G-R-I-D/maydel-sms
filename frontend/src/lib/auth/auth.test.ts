import { describe, expect, it } from "vitest";
import { canEnter, homeFor, safeNext } from "./roles";
import { REFRESH_LEEWAY_MS, jwtExpiry, needsRefresh } from "./token";
import { clientIp, forwardingHeaders } from "./forwarding";
import { ACCESS_COOKIE, REFRESH_COOKIE, USER_COOKIE, clearSession, decodeUser, encodeUser, writeSession } from "./cookies";
import type { AuthResponse, MeResponse } from "@/lib/api/types";

function jwt(payload: object): string {
  const b64 = (o: object) => Buffer.from(JSON.stringify(o)).toString("base64url");
  return `${b64({ alg: "HS256", typ: "JWT" })}.${b64(payload)}.signature`;
}

const me: MeResponse = {
  id: "8d1f4c1e-0000-4000-8000-000000000001",
  userName: "admin@maydel.test",
  fullName: "Ada Obi",
  email: "admin@maydel.test",
  roles: ["Admin"],
  mustChangePassword: false,
};

describe("roles", () => {
  it("sends staff to the console and parents/students to the portal", () => {
    expect(homeFor(["Admin"])).toBe("/admin");
    expect(homeFor(["Teacher"])).toBe("/admin");
    expect(homeFor(["Bursar"])).toBe("/admin");
    expect(homeFor(["Parent"])).toBe("/portal");
    expect(homeFor(["Student"])).toBe("/portal");
    expect(homeFor(["Parent", "Teacher"])).toBe("/admin");
    expect(homeFor([])).toBe("/login");
    expect(homeFor(["Applicant"])).toBe("/admissions");
    expect(homeFor(["Parent", "Applicant"])).toBe("/portal");
  });

  it("guards console and portal areas by role", () => {
    expect(canEnter("/admin", ["Teacher"])).toBe(true);
    expect(canEnter("/admin/audit", ["Parent"])).toBe(false);
    expect(canEnter("/admissions", ["Applicant"])).toBe(true);
    expect(canEnter("/admissions", ["Parent"])).toBe(true);
    expect(canEnter("/admissions", ["Teacher"])).toBe(false);
    expect(canEnter("/portal", ["Applicant"])).toBe(false);
    expect(canEnter("/admin", ["Applicant"])).toBe(false);
    expect(canEnter("/admissions/apply", [])).toBe(true);
    expect(canEnter("/portal", ["Student"])).toBe(true);
    expect(canEnter("/portal/results", ["Bursar"])).toBe(false);
    expect(canEnter("/exam/abc", ["Student"])).toBe(true);
    expect(canEnter("/exam/abc", ["Parent"])).toBe(false);
    expect(canEnter("/exam/abc", ["Teacher"])).toBe(false);
    expect(canEnter("/examples", ["Parent"])).toBe(true);
    expect(canEnter("/administrator", ["Parent"])).toBe(true);
    expect(canEnter("/change-password", ["Parent"])).toBe(true);
  });

  it("only accepts same-site relative next paths", () => {
    expect(safeNext("/admin/audit?page=2")).toBe("/admin/audit?page=2");
    expect(safeNext("https://evil.example")).toBeNull();
    expect(safeNext("//evil.example")).toBeNull();
    expect(safeNext("/\\evil.example")).toBeNull();
    expect(safeNext("/api/auth/logout")).toBeNull();
    expect(safeNext("/login")).toBeNull();
    expect(safeNext(undefined)).toBeNull();
  });
});

describe("token", () => {
  const now = Date.UTC(2026, 9, 13, 12, 0, 0);

  it("reads exp from a JWT", () => {
    expect(jwtExpiry(jwt({ exp: 1_800_000_000 }))).toBe(1_800_000_000_000);
    expect(jwtExpiry("not-a-jwt")).toBeNull();
    expect(jwtExpiry("a.%%%.c")).toBeNull();
  });

  it("refreshes when missing, unreadable or within two minutes of expiry", () => {
    expect(needsRefresh(undefined, now)).toBe(true);
    expect(needsRefresh("garbage", now)).toBe(true);
    expect(needsRefresh(jwt({ exp: (now + REFRESH_LEEWAY_MS - 1000) / 1000 }), now)).toBe(true);
    expect(needsRefresh(jwt({ exp: (now + 10 * 60 * 1000) / 1000 }), now)).toBe(false);
  });
});

describe("forwarding", () => {
  const h = (o: Record<string, string>) => new Headers(o);

  it("uses the right-most X-Forwarded-For hop (the one our proxy appended)", () => {
    expect(clientIp(h({ "x-forwarded-for": "6.6.6.6, 41.58.1.2" }))).toBe("41.58.1.2");
    expect(clientIp(h({ "x-forwarded-for": "::ffff:10.0.0.5" }))).toBe("10.0.0.5");
    expect(clientIp(h({ "x-real-ip": "41.58.1.3" }))).toBe("41.58.1.3");
    expect(clientIp(h({}))).toBeUndefined();
  });

  it("forwards IP and User-Agent", () => {
    expect(forwardingHeaders(h({ "x-forwarded-for": "41.58.1.2", "user-agent": "Mozilla/5.0" }))).toEqual({
      "X-Forwarded-For": "41.58.1.2",
      "User-Agent": "Mozilla/5.0",
    });
  });
});

describe("session cookies", () => {
  type Set = { name: string; value: string; options?: Record<string, unknown> };
  const jar = () => {
    const sets: Set[] = [];
    return { sets, set: (name: string, value: string, options?: Record<string, unknown>) => sets.push({ name, value, options }) };
  };

  it("round-trips the cached user", () => {
    expect(decodeUser(encodeUser(me))).toEqual(me);
    expect(decodeUser("not base64 json")).toBeNull();
    expect(decodeUser(undefined)).toBeNull();
  });

  it("writes httpOnly, SameSite=Lax cookies that expire with their tokens", () => {
    const auth: AuthResponse = {
      accessToken: "a",
      accessTokenExpiresAt: "2026-10-13T12:15:00Z",
      refreshToken: "r",
      refreshTokenExpiresAt: "2026-10-27T12:00:00Z",
      user: me,
    };
    const j = jar();
    writeSession(j, auth);
    const byName = Object.fromEntries(j.sets.map((s) => [s.name, s]));
    expect(byName[ACCESS_COOKIE].value).toBe("a");
    expect(byName[ACCESS_COOKIE].options).toMatchObject({ httpOnly: true, sameSite: "lax", path: "/", expires: new Date("2026-10-13T12:15:00Z") });
    expect(byName[REFRESH_COOKIE].options).toMatchObject({ httpOnly: true, expires: new Date("2026-10-27T12:00:00Z") });
    expect(byName[USER_COOKIE].options).toMatchObject({ httpOnly: true });
  });

  it("clears every session cookie", () => {
    const j = jar();
    clearSession(j);
    expect(j.sets.map((s) => s.name)).toEqual(expect.arrayContaining([ACCESS_COOKIE, REFRESH_COOKIE, USER_COOKIE]));
    expect(j.sets.every((s) => s.options?.maxAge === 0)).toBe(true);
  });
});
