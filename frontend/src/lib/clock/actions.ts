"use server";

import { revalidatePath } from "next/cache";
import { headers } from "next/headers";
import createClient from "openapi-fetch";
import type { paths } from "@/lib/api/schema";
import { apiBaseUrl } from "@/lib/api/config";
import { toProblem, unreachableProblem } from "@/lib/api/problem";
import { load, type Loaded } from "@/lib/api/server";
import { forwardingHeaders } from "@/lib/auth/forwarding";
import type { ClockResult, ClockSettings, ClockSettingsInput, KioskCode, StaffDay } from "./clock";

/** Staff: scan the kiosk code. The first scan of the day clocks in, the next clocks out. */
export async function clock(code: string, latitude?: number, longitude?: number): Promise<Loaded<ClockResult>> {
  const r = await load((api) => api.POST("/api/clock", { body: { code, latitude: latitude ?? null, longitude: longitude ?? null } }));
  if (r.ok) revalidatePath("/clock");
  return r;
}

/**
 * Kiosk (no sign-in): the current code for the kiosk link's token. Anonymous and never cached; a revoked
 * token comes back as a 401 Problem (not a sign-in redirect), so the screen can say the link was replaced.
 */
export async function kioskCode(token: string): Promise<Loaded<KioskCode>> {
  if (!/^[a-f0-9]{16,128}$/.test(token)) return { ok: false, problem: toProblem(401, { code: "clock.kiosk", detail: "This kiosk link isn't valid." }) };
  const api = createClient<paths>({
    baseUrl: apiBaseUrl(),
    headers: { Accept: "application/json", ...forwardingHeaders(await headers()) },
    fetch: (request: Request) => fetch(request, { cache: "no-store" }),
  });
  try {
    const { data, error, response } = await api.GET("/api/public/clock/kiosk", { params: { query: { token } } });
    return response.ok && data ? { ok: true, data } : { ok: false, problem: toProblem(response.status, error) };
  } catch {
    return { ok: false, problem: unreachableProblem() };
  }
}

/** Admin: hours, grace and the optional geofence. */
export async function saveClockSettings(body: ClockSettingsInput): Promise<Loaded<ClockSettings>> {
  const r = await load((api) => api.PUT("/api/clock/settings", { body }));
  if (r.ok) revalidatePath("/admin/settings/clock");
  return r;
}

/** Admin: a new kiosk link. The token is returned once; issuing again revokes the old link. */
export async function issueKioskLink(): Promise<Loaded<{ token: string }>> {
  const r = await load((api) => api.POST("/api/clock/kiosk-link"));
  if (r.ok) revalidatePath("/admin/settings/clock");
  return r;
}

/** Admin: correct one staff member's day (times are WAT instants; a note is required and audited). */
export async function correctDay(staffId: string, date: string, body: { clockInAt: string | null; clockOutAt: string | null; note: string }): Promise<Loaded<StaffDay>> {
  const r = await load((api) => api.PUT("/api/clock/days/{staffId}/{date}", { params: { path: { staffId, date } }, body }));
  if (r.ok) revalidatePath("/admin/staff/attendance");
  return r;
}
