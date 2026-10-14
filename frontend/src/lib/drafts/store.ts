/**
 * Unsaved-edit drafts, kept in sessionStorage so they survive a reload or a sign-in round-trip but not
 * closing the tab (teachers share school PCs; drafts hold children's data). Keys are namespaced by
 * user, expire after DRAFT_TTL_MS, and are wiped on sign-out or when a different user signs in.
 * Never put passwords, temporary credentials or tokens in a draft. Storage failures (private mode,
 * full quota) just mean "no draft".
 */

export const DRAFT_PREFIX = "draft:";
export const DRAFT_OWNER_KEY = "draft-owner";
export const DRAFT_TTL_MS = 12 * 60 * 60 * 1000;

export type Draft<T> = { value: T; savedAt: number; base: string | null };
type Storage = Pick<globalThis.Storage, "getItem" | "setItem" | "removeItem" | "key" | "length">;

function storage(): Storage | null {
  try {
    return typeof window === "undefined" ? null : window.sessionStorage;
  } catch {
    return null;
  }
}

export function draftKey(userId: string, ...parts: (string | number)[]): string {
  return `${DRAFT_PREFIX}${userId}:${parts.join(":")}`;
}

export function readDraft<T>(key: string, now = Date.now(), store: Storage | null = storage()): Draft<T> | null {
  if (!store) return null;
  try {
    const raw = store.getItem(key);
    if (!raw) return null;
    const d = JSON.parse(raw) as Partial<Draft<T>>;
    if (typeof d.savedAt !== "number" || !("value" in d) || now - d.savedAt > DRAFT_TTL_MS) {
      store.removeItem(key);
      return null;
    }
    return { value: d.value as T, savedAt: d.savedAt, base: typeof d.base === "string" ? d.base : null };
  } catch {
    return null;
  }
}

export function writeDraft<T>(key: string, value: T, base: string | null, now = Date.now(), store: Storage | null = storage()): void {
  try {
    store?.setItem(key, JSON.stringify({ value, savedAt: now, base } satisfies Draft<T>));
  } catch {
    // quota / private mode: no draft
  }
}

export function removeDraft(key: string, store: Storage | null = storage()): void {
  try {
    store?.removeItem(key);
  } catch {
    // ignore
  }
}

/** Removes every draft (sign-out, user switch) and, optionally, only the expired ones. */
export function clearDrafts(opts: { expiredOnly?: boolean; now?: number } = {}, store: Storage | null = storage()): void {
  if (!store) return;
  try {
    const keys: string[] = [];
    for (let i = 0; i < store.length; i++) {
      const k = store.key(i);
      if (k?.startsWith(DRAFT_PREFIX)) keys.push(k);
    }
    for (const k of keys) {
      if (opts.expiredOnly) readDraft(k, opts.now ?? Date.now(), store); // drops it when stale
      else store.removeItem(k);
    }
  } catch {
    // ignore
  }
}

/** Called once per page load in the console: a different user on this tab means the old drafts go. */
export function claimDrafts(userId: string, store: Storage | null = storage()): void {
  if (!store) return;
  try {
    if (store.getItem(DRAFT_OWNER_KEY) !== userId) {
      clearDrafts({}, store);
      store.setItem(DRAFT_OWNER_KEY, userId);
    } else {
      clearDrafts({ expiredOnly: true }, store);
    }
  } catch {
    // ignore
  }
}

/** Stable fingerprint of the server state a draft was made against, to spot changes underneath it. */
export function fingerprint(value: unknown): string {
  return JSON.stringify(value ?? null);
}
