import type { components } from "@/lib/api/schema";

/** E12.8: the user's own notifications (payments, overdue fees; homework and messages later). */
export type Notification = components["schemas"]["NotificationDto"];
export type NotificationKind = components["schemas"]["NotificationKind"];
/** E12.1: school notices for the user's audience. */
export type PortalNotice = components["schemas"]["PortalNotice"];

export type Bell = { notifications: Notification[]; notices: PortalNotice[]; unread: number; failed: boolean };

/** "99+" past 99, nothing at 0. */
export function badgeText(unread: number): string | null {
  return unread <= 0 ? null : unread > 99 ? "99+" : String(unread);
}

/** Only same-site paths are followed from a notification, so a stored link can't send users elsewhere. */
export function safeLink(url: string | null | undefined): string | null {
  return url && url.startsWith("/") && !url.startsWith("//") && !url.startsWith("/\\") ? url : null;
}

const rtf = new Intl.RelativeTimeFormat("en-NG", { numeric: "auto" });
const dayFmt = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", timeZone: "Africa/Lagos" });

/** "5 minutes ago", "yesterday", then "12 Oct". */
export function ago(iso: string, now = new Date()): string {
  const s = Math.round((new Date(iso).getTime() - now.getTime()) / 1000);
  if (s > -60) return "just now";
  if (s > -3600) return rtf.format(Math.round(s / 60), "minute");
  if (s > -86400) return rtf.format(Math.round(s / 3600), "hour");
  if (s > -7 * 86400) return rtf.format(Math.round(s / 86400), "day");
  return dayFmt.format(new Date(iso));
}
