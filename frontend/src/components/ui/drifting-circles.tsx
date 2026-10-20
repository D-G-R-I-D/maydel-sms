import Image from "next/image";
import { cn } from "./primitives";

/**
 * The school crest as a faint watermark behind a section (about 5% opacity), off-centre so it
 * never sits under text. The section needs `relative overflow-hidden` and its content `relative z-10`.
 */
export function CrestWatermark({ side = "right", className }: { side?: "left" | "right"; className?: string }) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        "pointer-events-none absolute top-1/2 z-0 hidden w-[26rem] -translate-y-1/2 opacity-[0.05] select-none sm:block",
        side === "right" ? "-right-24 rotate-[8deg]" : "-left-24 -rotate-[8deg]",
        className,
      )}
    >
      <Image src="/brand/crest.svg" alt="" width={416} height={520} className="h-auto w-full" />
    </div>
  );
}

/**
 * Decorative drifting circles for brand panels (CSS in globals.css, `.drift-*`): rings and dots that
 * wander, glow orbs that breathe and ripples spreading from the corner, "the well spring". Purely
 * visual (aria-hidden, no pointer events) and still under prefers-reduced-motion.
 * `tone="dark"` sits on the navy panel; `tone="light"` on page backgrounds (and adapts in dark mode).
 * The parent needs `relative` and its content a higher z-index (e.g. `relative z-10`).
 */
export function DriftingCircles({ tone = "dark", density = "full", className }: { tone?: "dark" | "light"; density?: "full" | "light"; className?: string }) {
  return (
    <div className={cn("drift-field", className)} data-tone={tone} aria-hidden="true">
      <span className="drift-orb drift-orb-a drift-o1" />
      <span className="drift-orb drift-orb-b drift-o2" />
      <span className="drift-ripple drift-r1" />
      <span className="drift-ripple drift-r2" />
      <span className="drift-ripple drift-r3" />
      <span className="drift-ring drift-ring-dashed drift-7" />
      <span className="drift-ring drift-1" />
      <span className="drift-ring drift-ring-gold drift-3" />
      <span className="drift-ring drift-6" />
      <span className="drift-dot drift-d1" />
      <span className="drift-dot drift-d3" />
      {density === "full" && (
        <>
          <span className="drift-ring drift-2" />
          <span className="drift-ring drift-4" />
          <span className="drift-ring drift-ring-gold drift-5" />
          <span className="drift-dot drift-d2" />
          <span className="drift-dot drift-d4" />
          <span className="drift-dot drift-d5" />
        </>
      )}
    </div>
  );
}
