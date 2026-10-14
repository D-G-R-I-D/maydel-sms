"use client";

import { usePathname, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { cn } from "./primitives";
import { SchoolLoader } from "./school-loader";

type Phase = "idle" | "loading" | "done";

/** True for a plain left-click on a same-site link that will navigate within the app. */
function isInternalNavigation(e: MouseEvent): boolean {
  if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return false;
  const a = (e.target as Element | null)?.closest?.("a[href]");
  if (!(a instanceof HTMLAnchorElement)) return false;
  if (a.target && a.target !== "_self") return false;
  if (a.hasAttribute("download")) return false;
  const url = new URL(a.href, window.location.href);
  if (url.origin !== window.location.origin || url.pathname.startsWith("/api/")) return false;
  const here = window.location.pathname + window.location.search;
  return url.pathname + url.search !== here;
}

/**
 * Slim brand-coloured bar at the top during client navigations, plus the small crest loader if a
 * page takes longer than ~1.5 s. Finishes when the route (path or query) actually changes.
 */
export function NavigationProgress() {
  const pathname = usePathname();
  const search = useSearchParams();
  const [phase, setPhase] = useState<Phase>("idle");
  const [slow, setSlow] = useState(false);
  const timers = useRef<ReturnType<typeof setTimeout>[]>([]);
  const route = `${pathname}?${search.toString()}`;
  const [lastRoute, setLastRoute] = useState(route);

  const clear = () => {
    timers.current.forEach(clearTimeout);
    timers.current = [];
  };

  // Route changed → finish the bar (adjusting state during render, not in an effect).
  if (route !== lastRoute) {
    setLastRoute(route);
    if (phase === "loading") {
      setPhase("done");
      setSlow(false);
    }
  }

  useEffect(() => {
    if (phase !== "done") return;
    clear();
    const t = setTimeout(() => setPhase("idle"), 320);
    timers.current.push(t);
    return () => clearTimeout(t);
  }, [phase]);

  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (!isInternalNavigation(e)) return;
      clear();
      setSlow(false);
      setPhase("loading");
      timers.current.push(setTimeout(() => setSlow(true), 1500));
      // Safety net: never leave the bar hanging (e.g. navigation cancelled).
      timers.current.push(
        setTimeout(() => {
          setPhase((p) => (p === "loading" ? "done" : p));
          setSlow(false);
        }, 15_000),
      );
    };
    document.addEventListener("click", onClick, true);
    return () => {
      document.removeEventListener("click", onClick, true);
      clear();
    };
  }, []);

  if (phase === "idle") return null;
  return (
    <>
      <div aria-hidden className="pointer-events-none fixed inset-x-0 top-0 z-[60] h-0.5">
        <div
          className={cn(
            "h-full origin-left bg-gradient-to-r from-brand via-brand to-[#d9b16a]",
            phase === "loading" ? "[animation:nav-trickle_6s_cubic-bezier(0.1,0.7,0.2,1)_forwards]" : "scale-x-100 opacity-0 transition-[transform,opacity] duration-300",
          )}
        />
      </div>
      {phase === "loading" && slow && (
        <div role="status" aria-live="polite" className="animate-fade fixed top-3 right-3 z-[60] rounded-full border border-border bg-surface p-1.5 shadow-lg">
          <SchoolLoader size="sm" />
          <span className="sr-only">Loading page…</span>
        </div>
      )}
    </>
  );
}
