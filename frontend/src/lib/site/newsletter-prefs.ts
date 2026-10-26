/** Per-browser newsletter memory for the nudge. Storage can be unavailable (private mode); then we don't nag. */
export const SUBSCRIBED_KEY = "nl:subscribed";
export const NEXT_KEY = "nl:next";

export function readPref(key: string): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function writePref(key: string, value: string) {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    // Ignore: without storage the nudge simply returns on the next visit.
  }
}

/** Once someone signs up (footer, nudge or confirm page), stop nudging in this browser. */
export function markSubscribed() {
  writePref(SUBSCRIBED_KEY, "1");
}
