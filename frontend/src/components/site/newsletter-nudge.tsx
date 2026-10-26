"use client";

import { useEffect, useState } from "react";
import { Mail, X } from "lucide-react";
import { cn } from "@/components/ui/primitives";
import { NewsletterSignup } from "./newsletter-signup";
import { NEXT_KEY as NEXT, SUBSCRIBED_KEY as SUBSCRIBED, readPref as read, writePref as write } from "@/lib/site/newsletter-prefs";

const FIRST_DELAY_MS = 20_000;
const MIN_GAP_MS = 5 * 60_000;
const MAX_GAP_MS = 10 * 60_000;

/**
 * A small, dismissible newsletter card in the corner (never a blocking pop-up). It first appears after
 * ~20 s, and after "Not now" comes back 5–10 minutes later, until this browser signs up.
 */
export function NewsletterNudge() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (read(SUBSCRIBED)) return;
    if (!read(NEXT)) write(NEXT, String(Date.now() + FIRST_DELAY_MS));
    const check = () => {
      if (read(SUBSCRIBED) || document.visibilityState !== "visible") return;
      if (Date.now() >= Number(read(NEXT) ?? 0)) setOpen(true);
    };
    const id = window.setInterval(check, 5_000);
    const first = window.setTimeout(check, 1_000);
    return () => {
      window.clearInterval(id);
      window.clearTimeout(first);
    };
  }, []);

  const dismiss = () => {
    write(NEXT, String(Date.now() + MIN_GAP_MS + Math.round(Math.random() * (MAX_GAP_MS - MIN_GAP_MS))));
    setOpen(false);
  };

  if (!open) return null;
  return (
    <aside
      aria-label="School newsletter"
      className={cn("animate-rise fixed bottom-4 left-4 z-40 w-[min(22rem,calc(100vw-2rem))] rounded-card border border-border bg-surface p-4 text-text shadow-2xl", "max-lg:bottom-20")}
    >
      <button type="button" onClick={dismiss} className="absolute top-2 right-2 rounded-input p-1.5 text-muted hover:bg-surface-2 hover:text-text" aria-label="Not now">
        <X className="size-4" aria-hidden />
      </button>
      <p className="flex items-center gap-2 pr-6 font-display text-lg font-semibold">
        <Mail className="size-5 text-brand" aria-hidden />
        Stay in the loop
      </p>
      <p className="mt-1 mb-3 text-sm text-muted">News, events and dates from Maydel Spring Schools, straight to your inbox.</p>
      <NewsletterSignup idPrefix="nudge" />
      <button type="button" onClick={dismiss} className="mt-2 text-xs text-muted underline-offset-2 hover:underline">
        Not now
      </button>
    </aside>
  );
}
