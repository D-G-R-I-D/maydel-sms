"use client";

import { createContext, useCallback, useContext, useEffect, useId, useRef, useState } from "react";
import { setUnsaved } from "./guard";
import { decideRestore } from "./rules";
import { draftKey, fingerprint, readDraft, removeDraft, writeDraft } from "./store";

/** The signed-in user's id, provided by <DraftScope> in the console layout. */
export const DraftUserContext = createContext<string | null>(null);

const DEBOUNCE_MS = 500;

export type DraftState = {
  /** When the restored draft was saved; null when nothing was restored (or it was dismissed). */
  restoredAt: number | null;
  /** The server copy changed after the draft was made. */
  changedUnderneath: boolean;
  /** Forget the draft (after a successful save, or with Discard). */
  clear: () => void;
};

/**
 * Keeps unsaved edits in sessionStorage so a reload, a session-refresh redirect or a crash doesn't lose
 * them. `scope` identifies the screen and records (e.g. ["comment", kind, studentId, termId]);
 * `server` is the saved state the edit started from (fingerprinted to spot changes underneath).
 * Pass `editable: false` for read-only screens: any stale draft is dropped.
 */
export function useDraft<T>({
  scope,
  value,
  dirty,
  server,
  editable = true,
  onRestore,
}: {
  scope: (string | number)[];
  value: T;
  dirty: boolean;
  server: unknown;
  editable?: boolean;
  onRestore: (value: T) => void;
}): DraftState {
  const userId = useContext(DraftUserContext);
  const key = userId ? draftKey(userId, ...scope) : null;
  const base = fingerprint(server);
  const guardId = useId();
  const [restored, setRestored] = useState<{ at: number; changed: boolean } | null>(null);
  const checked = useRef<string | null>(null);
  const pending = useRef<(() => void) | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Restore once per key, before any write can overwrite the stored draft. sessionStorage only exists
  // after hydration, so this has to be an effect that sets state.
  useEffect(() => {
    if (!key || checked.current === key) return;
    checked.current = key;
    const draft = readDraft<T>(key);
    const decision = decideRestore(draft, { base, editable, value });
    if (decision === "drop") removeDraft(key);
    if (draft && (decision === "restore" || decision === "restore-changed")) {
      onRestore(draft.value);
      // eslint-disable-next-line react-hooks/set-state-in-effect -- one-off restore from browser storage
      setRestored({ at: draft.savedAt, changed: decision === "restore-changed" });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- runs when the record changes, not on every edit
  }, [key]);

  const flush = useCallback(() => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    pending.current?.();
    pending.current = null;
  }, []);

  // Debounced write (or removal once nothing is unsaved). Flushed on pagehide and unmount.
  useEffect(() => {
    if (!key || checked.current !== key || !editable) return;
    pending.current = dirty ? () => writeDraft(key, value, base) : () => removeDraft(key);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(flush, DEBOUNCE_MS);
  }, [key, value, dirty, base, editable, flush]);

  useEffect(() => {
    window.addEventListener("pagehide", flush);
    return () => {
      window.removeEventListener("pagehide", flush);
      flush();
    };
  }, [flush]);

  useEffect(() => {
    setUnsaved(guardId, editable && dirty);
    return () => setUnsaved(guardId, false);
  }, [guardId, editable, dirty]);

  const clear = useCallback(() => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
    pending.current = null;
    if (key) removeDraft(key);
    setRestored(null);
  }, [key]);

  return { restoredAt: restored?.at ?? null, changedUnderneath: restored?.changed ?? false, clear };
}
