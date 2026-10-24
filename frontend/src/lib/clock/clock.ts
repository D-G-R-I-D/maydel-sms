import type { components } from "@/lib/api/schema";

/** Staff clock-in/out with a rotating kiosk QR (handoff: docs/handoffs/staff-clock-in.md). */
export type StaffDay = components["schemas"]["StaffDayDto"];
export type ClockResult = components["schemas"]["ClockResult"];
export type ClockSettings = components["schemas"]["ClockSettingsDto"];
export type ClockSettingsInput = components["schemas"]["ClockSettingsInput"];
export type KioskCode = components["schemas"]["KioskCode"];
export type DayBoard = components["schemas"]["DayBoard"];
export type AttendanceReport = components["schemas"]["AttendanceReport"];
export type StaffSummary = components["schemas"]["StaffSummary"];
export type DayState = components["schemas"]["DayState"];

export const ClockCodes = {
  code: "clock.code",
  notStaff: "clock.not_staff",
  weekend: "clock.weekend",
  tooFar: "clock.too_far",
  tooSoon: "clock.too_soon",
  done: "clock.done",
  twice: "clock.twice",
  kiosk: "clock.kiosk",
} as const;

const LAGOS = "Africa/Lagos";
const timeFmt = new Intl.DateTimeFormat("en-NG", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: LAGOS });
const clockFmt = new Intl.DateTimeFormat("en-NG", { hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false, timeZone: LAGOS });
const dayFmt = new Intl.DateTimeFormat("en-NG", { weekday: "short", day: "numeric", month: "short", timeZone: "UTC" });
const longDayFmt = new Intl.DateTimeFormat("en-NG", { weekday: "long", day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });

/** "07:42" (WAT) for an instant, or an em dash. */
export function watTime(iso: string | null | undefined): string {
  return iso ? timeFmt.format(new Date(iso)) : "—";
}

/** "07:42:15" (WAT), for the kiosk's live clock. */
export function watClock(now: Date): string {
  return clockFmt.format(now);
}

/** "Mon, 26 Oct" for a calendar date ("2026-10-26"). */
export function shortDay(date: string): string {
  return dayFmt.format(new Date(`${date}T00:00:00Z`));
}

/** "Monday, 26 October 2026" for a calendar date. */
export function longDay(date: string): string {
  return longDayFmt.format(new Date(`${date}T00:00:00Z`));
}

/** Today's date in Abuja ("2026-10-26"); WAT is UTC+1 all year. */
export function todayWat(now = new Date()): string {
  return new Date(now.getTime() + 3600 * 1000).toISOString().slice(0, 10);
}

/** A calendar date shifted by whole days. */
export function addDays(date: string, days: number): string {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

export const isDate = (v: string | undefined): v is string => Boolean(v && /^\d{4}-\d{2}-\d{2}$/.test(v) && !Number.isNaN(Date.parse(`${v}T00:00:00Z`)));

/** "8.4 h", or an em dash. */
export function hours(h: number | null | undefined): string {
  return h == null ? "—" : `${h.toLocaleString("en-NG", { maximumFractionDigits: 1 })} h`;
}

/** "07:30:00" → "07:30". */
export const hhmm = (t: string | null | undefined) => (t ? t.slice(0, 5) : "—");

export const stateTone: Record<DayState, "success" | "warning" | "neutral" | "danger"> = { In: "success", Late: "warning", Out: "neutral", Absent: "danger" };
export const stateLabel: Record<DayState, string> = { In: "In", Late: "Late", Out: "Clocked out", Absent: "Absent" };

/** The big line on the result card after a scan. */
export function resultHeadline(r: ClockResult): { title: string; detail: string } {
  if (r.clockedIn) {
    return { title: `Clocked in at ${watTime(r.day.clockInAt)}`, detail: r.day.isLate ? "You're marked late today." : "On time. Have a good day." };
  }
  return { title: `Clocked out at ${watTime(r.day.clockOutAt)}`, detail: r.day.hoursWorked != null ? `${hours(r.day.hoursWorked)} today. See you tomorrow.` : "See you tomorrow." };
}

/**
 * The kiosk's `url` is relative in local dev (no PortalUrl on the server) and absolute in production.
 * Only http(s) URLs on the page's own origin or the server's are encoded; anything else is refused.
 */
export function resolveKioskUrl(url: string, origin: string): string | null {
  try {
    const u = new URL(url, origin);
    return u.protocol === "https:" || u.protocol === "http:" ? u.toString() : null;
  } catch {
    return null;
  }
}

/** The scan code from `?c=` ("window.signature"): short, URL-safe text only, so a crafted link can't smuggle anything else. */
export function cleanCode(c: string | string[] | undefined): string | null {
  const v = Array.isArray(c) ? c[0] : c;
  return v && /^[A-Za-z0-9._-]{4,128}$/.test(v) ? v : null;
}

/** "HH:mm" from a time input → "HH:mm:00" for the API, or null when malformed. */
export function toApiTime(v: string): string | null {
  return /^([01]\d|2[0-3]):[0-5]\d$/.test(v) ? `${v}:00` : null;
}

/** Why the browser gave no location (GeolocationPositionError codes), in words staff can act on. */
export function locationError(code: number | undefined): string {
  if (code === 1) return "Location is blocked for this site. Tap the lock icon by the address, allow Location, then try again.";
  if (code === 2) return "This device couldn't find its position. Turn on Location (GPS) in the phone's settings, then try again.";
  if (code === 3) return "Finding your location took too long. Move nearer a window or outside, then try again.";
  return "This browser can't share its location. Try Chrome or Safari on your phone.";
}

/** True for addresses a phone can't reach from another device (this computer only). */
export function isLocalOnly(url: string): boolean {
  try {
    const h = new URL(url).hostname;
    return h === "localhost" || h === "::1" || h === "[::1]" || h.startsWith("127.") || h.endsWith(".localhost");
  } catch {
    return false;
  }
}
