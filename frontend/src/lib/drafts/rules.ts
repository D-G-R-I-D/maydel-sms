import type { Draft } from "./store";

export type RestoreDecision = "none" | "restore" | "restore-changed" | "drop";

/**
 * What to do with a stored draft when a screen opens:
 * - read-only now (e.g. the report was published meanwhile) → drop it;
 * - same value as the server already has → drop it (nothing unsaved);
 * - server data changed since the draft was made → restore, but flag it so the user checks;
 * - otherwise restore.
 */
export function decideRestore<T>(draft: Draft<T> | null, current: { base: string | null; editable: boolean; value: T }): RestoreDecision {
  if (!draft) return "none";
  if (!current.editable) return "drop";
  if (JSON.stringify(draft.value) === JSON.stringify(current.value)) return "drop";
  if (draft.base !== null && current.base !== null && draft.base !== current.base) return "restore-changed";
  return "restore";
}

const timeFormat = new Intl.DateTimeFormat("en-NG", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });
const dayFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", timeZone: "Africa/Lagos" });

/** "14:05", or "13 Oct, 22:40" when it wasn't today (Lagos time). */
export function draftTime(savedAt: number, now = Date.now()): string {
  const sameDay = dayFormat.format(savedAt) === dayFormat.format(now);
  return sameDay ? timeFormat.format(savedAt) : `${dayFormat.format(savedAt)}, ${timeFormat.format(savedAt)}`;
}
