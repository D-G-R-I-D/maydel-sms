"use client";

import { useEffect } from "react";
import { hasUnsaved } from "@/lib/drafts/guard";
import { claimDrafts, clearDrafts } from "@/lib/drafts/store";
import { DraftUserContext } from "@/lib/drafts/use-draft";

export const SIGN_OUT_MESSAGE = "Sign out? Your unsaved changes on this page will be lost.";
export const LEAVE_MESSAGE = "You have unsaved changes. Leave this page? They stay as a draft in this tab until you save or discard them.";

/**
 * Console-wide draft plumbing: whose drafts these are (a different user signing in on this tab wipes
 * the previous user's; signing out wipes them too), and a warning before leaving with unsaved edits, on unload and on in-app links.
 */
export function DraftScope({ userId, children }: { userId: string; children: React.ReactNode }) {
  useEffect(() => claimDrafts(userId), [userId]);

  useEffect(() => {
    let leaving = false;
    const beforeUnload = (e: BeforeUnloadEvent) => {
      if (leaving || !hasUnsaved()) return;
      e.preventDefault();
      e.returnValue = ""; // older browsers need it set
    };
    const click = (e: MouseEvent) => {
      if (!hasUnsaved() || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
      const a = (e.target as Element | null)?.closest?.("a[href]");
      if (!(a instanceof HTMLAnchorElement) || a.target === "_blank" || a.hasAttribute("download")) return;
      const url = new URL(a.href, location.href);
      if (url.origin !== location.origin || (url.pathname === location.pathname && url.search === location.search)) return;
      if (!window.confirm(LEAVE_MESSAGE)) {
        e.preventDefault();
        e.stopPropagation();
      }
    };
    const submit = (e: SubmitEvent) => {
      const form = e.target;
      if (!(form instanceof HTMLFormElement) || !form.action.endsWith("/api/auth/logout")) return;
      if (hasUnsaved() && !window.confirm(SIGN_OUT_MESSAGE)) {
        e.preventDefault();
        return;
      }
      leaving = true;
      clearDrafts();
    };
    window.addEventListener("beforeunload", beforeUnload);
    document.addEventListener("click", click, true);
    document.addEventListener("submit", submit, true);
    return () => {
      window.removeEventListener("beforeunload", beforeUnload);
      document.removeEventListener("click", click, true);
      document.removeEventListener("submit", submit, true);
    };
  }, []);

  return <DraftUserContext.Provider value={userId}>{children}</DraftUserContext.Provider>;
}
