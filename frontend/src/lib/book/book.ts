import type { components } from "@/lib/api/schema";

/** E17.2 communication book: one running notebook per student between the school and the family. */
export type BookSummary = components["schemas"]["BookSummary"];
export type BookPage = components["schemas"]["BookPage"];
export type BookEntry = components["schemas"]["BookEntryDto"];

export const BookCodes = { forbidden: "book.forbidden", notAuthor: "book.not_author", editWindow: "book.edit_window" } as const;
export const MAX_BODY = 4000;

const timeFmt = new Intl.DateTimeFormat("en-NG", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });
const dayFmt = new Intl.DateTimeFormat("en-NG", { weekday: "long", day: "numeric", month: "long", timeZone: "Africa/Lagos" });
const keyFmt = new Intl.DateTimeFormat("en-CA", { year: "numeric", month: "2-digit", day: "2-digit", timeZone: "Africa/Lagos" });

/** "07:42" (WAT). */
export const entryTime = (iso: string) => timeFmt.format(new Date(iso));
/** "2026-10-27" in WAT, to group notes by day. */
export const dayKey = (iso: string) => keyFmt.format(new Date(iso));
/** "Tuesday, 27 October". */
export const dayHeading = (iso: string) => dayFmt.format(new Date(iso));

/** Notes grouped by WAT day, keeping chat order (oldest first). */
export function groupByDay(entries: readonly BookEntry[]): { key: string; heading: string; entries: BookEntry[] }[] {
  const out: { key: string; heading: string; entries: BookEntry[] }[] = [];
  for (const e of entries) {
    const key = dayKey(e.createdAt);
    const last = out[out.length - 1];
    if (last?.key === key) last.entries.push(e);
    else out.push({ key, heading: dayHeading(e.createdAt), entries: [e] });
  }
  return out;
}
