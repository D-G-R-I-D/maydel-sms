"use client";

import { useEffect, useRef, useState } from "react";
import Image from "next/image";
import QRCode from "qrcode";
import { Link2Off, TriangleAlert, WifiOff } from "lucide-react";
import { kioskCode } from "@/lib/clock/actions";
import { ClockCodes, isLocalOnly, resolveKioskUrl, watClock, type KioskCode } from "@/lib/clock/clock";
import type { Problem } from "@/lib/api/problem";
import { DriftingCircles } from "@/components/ui/drifting-circles";

const WINDOW_SECONDS = 30;
/** A few pixels' shift per minute, so nothing sits on the same pixels all day. */
const DRIFT = ["translate-x-0 translate-y-0", "translate-x-1.5 -translate-y-1", "-translate-x-1 translate-y-1.5", "translate-x-1 translate-y-1", "-translate-x-1.5 -translate-y-1.5"];

/**
 * The reception screen: a QR that changes every 30 seconds, the school's name and a live WAT clock.
 * It fetches the next code just before the current one expires, keeps the screen awake where the browser
 * allows, and drifts the content a few pixels each minute so a tablet left on all day doesn't burn in.
 */
export function KioskScreen({ token, publicOrigin }: { token: string; publicOrigin: string | null }) {
  const [code, setCode] = useState<KioskCode | null>(null);
  const [svg, setSvg] = useState<string | null>(null);
  const [target, setTarget] = useState<string | null>(null);
  const [problem, setProblem] = useState<Problem | null>(null);
  // Null until mounted: the server's time would never match the browser's on hydration.
  const [now, setNow] = useState<Date | null>(null);
  const timer = useRef<number | undefined>(undefined);

  useEffect(() => {
    let cancelled = false;
    const next = async () => {
      let delay = 10_000;
      try {
        const r = await kioskCode(token);
        if (cancelled) return;
        if (r.ok) {
          const url = resolveKioskUrl(r.data.url, publicOrigin ?? window.location.origin);
          if (url) {
            setTarget(url);
            setSvg(await QRCode.toString(url, { type: "svg", margin: 1, errorCorrectionLevel: "M", color: { dark: "#0f2a44", light: "#ffffff" } }));
            setCode(r.data);
            setProblem(null);
          }
          delay = Math.max(3_000, new Date(r.data.expiresAt).getTime() - Date.now() - 2_000);
        } else {
          setProblem(r.problem);
          // A replaced link never comes back; anything else (network, rate limit) is retried.
          if (r.problem.status === 401) return;
        }
      } catch {
        // Retried below.
      }
      if (!cancelled) timer.current = window.setTimeout(next, delay);
    };
    void next();
    return () => {
      cancelled = true;
      window.clearTimeout(timer.current);
    };
  }, [token, publicOrigin]);

  useEffect(() => {
    const tick = () => setNow(new Date());
    const first = window.setTimeout(tick, 0);
    const id = window.setInterval(tick, 1000);
    return () => {
      window.clearTimeout(first);
      window.clearInterval(id);
    };
  }, []);

  // Keep the screen on while the page is visible (re-acquired when the tab comes back).
  useEffect(() => {
    let lock: WakeLockSentinel | null = null;
    const acquire = async () => {
      try {
        if (document.visibilityState === "visible" && "wakeLock" in navigator) lock = await navigator.wakeLock.request("screen");
      } catch {
        // Not supported or not allowed; the tablet's own settings apply.
      }
    };
    void acquire();
    document.addEventListener("visibilitychange", acquire);
    return () => {
      document.removeEventListener("visibilitychange", acquire);
      void lock?.release().catch(() => undefined);
    };
  }, []);

  const replaced = problem?.status === 401 || problem?.code === ClockCodes.kiosk;
  const left = code && now ? Math.max(0, Math.min(WINDOW_SECONDS, (new Date(code.expiresAt).getTime() - now.getTime()) / 1000)) : 0;
  const drift = DRIFT[(now?.getMinutes() ?? 0) % DRIFT.length];

  return (
    <main id="main" className="brand-panel relative flex min-h-dvh items-center justify-center overflow-hidden p-6 text-white">
      <DriftingCircles density="light" />
      <div className={`relative z-10 flex w-full max-w-3xl flex-col items-center gap-6 text-center transition-transform duration-[3000ms] ${drift}`}>
        <div className="flex items-center gap-3">
          <span className="inline-flex size-14 items-center justify-center rounded-full bg-white/95 p-2 shadow-lg">
            <Image src="/brand/crest.svg" alt="" width={36} height={45} className="h-auto w-9" />
          </span>
          <div className="text-left">
            <p className="font-display text-2xl font-semibold">{code?.school ?? "Maydel Spring Schools"}</p>
            <p className="text-sm tracking-[0.18em] text-gold uppercase">Staff clock-in</p>
          </div>
        </div>

        <p className="tabular font-display text-6xl font-semibold sm:text-7xl" aria-live="off">
          {now ? watClock(now) : "--:--:--"}
          <span className="ml-2 align-middle text-base font-normal text-white/70">WAT</span>
        </p>

        {replaced ? (
          <div className="max-w-md space-y-2 rounded-card bg-white/10 p-6" role="alert">
            <Link2Off className="mx-auto size-10 text-gold" aria-hidden />
            <p className="font-display text-xl font-semibold">This kiosk link was replaced</p>
            <p className="text-white/80">Ask the school admin to open the new link from Settings → Staff clock-in.</p>
          </div>
        ) : !code || !svg ? (
          <div className="flex size-72 items-center justify-center rounded-card bg-white/10" role="status">
            {problem ? (
              <span className="space-y-2">
                <WifiOff className="mx-auto size-8 text-gold" aria-hidden />
                <span className="block text-sm text-white/80">Can&apos;t reach the school server. Retrying…</span>
              </span>
            ) : (
              <span className="text-sm text-white/80">Loading the code…</span>
            )}
          </div>
        ) : (
          <div className="relative">
            <ExpiryRing fraction={left / WINDOW_SECONDS} />
            <div
              className="relative m-5 size-64 rounded-card bg-white p-3 shadow-2xl sm:size-80 [&>svg]:size-full"
              role="img"
              aria-label="Scan this code with your phone to clock in or out"
              // The SVG comes from the QR library, built from the API's own URL.
              dangerouslySetInnerHTML={{ __html: svg }}
            />
          </div>
        )}

        {!replaced && (
          <div className="space-y-1">
            <p className="text-lg">Scan with your phone camera to clock in or out.</p>
            <p className="tabular text-sm text-white/70">{code ? `New code in ${Math.ceil(left)} s` : " "}</p>
            {problem && code && <p className="text-sm text-gold">Connection lost; the code may be out of date. Retrying…</p>}
            {target && (
              <p className="text-xs text-white/60">
                Opens <span className="font-mono">{new URL(target).host}</span>
              </p>
            )}
            {target && isLocalOnly(target) && (
              <p className="mx-auto mt-2 flex max-w-md items-start gap-2 rounded-input bg-gold/15 p-3 text-left text-sm text-gold" role="alert">
                <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                Phones can&apos;t open &quot;localhost&quot;. Open this kiosk through the school&apos;s web address, a dev tunnel, or this computer&apos;s Wi-Fi address (e.g. http://192.168.x.x:3000) instead.
              </p>
            )}
          </div>
        )}
      </div>
    </main>
  );
}

/** A gold ring around the QR that empties as the code's 30 seconds run out. */
function ExpiryRing({ fraction }: { fraction: number }) {
  const r = 48;
  const c = 2 * Math.PI * r;
  return (
    <svg viewBox="0 0 100 100" className="absolute inset-0 size-full -rotate-90" aria-hidden>
      <circle cx="50" cy="50" r={r} fill="none" stroke="currentColor" strokeWidth="1.2" className="text-white/15" />
      <circle cx="50" cy="50" r={r} fill="none" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" strokeDasharray={c} strokeDashoffset={c * (1 - fraction)} className="text-gold transition-[stroke-dashoffset] duration-1000 ease-linear" />
    </svg>
  );
}
