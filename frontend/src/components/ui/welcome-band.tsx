import type { ReactNode } from "react";
import { DriftingCircles } from "./drifting-circles";
import { cn } from "./primitives";

/**
 * The navy welcome band at the top of a dashboard (portal, console), matching the school's emails:
 * a small gold eyebrow, the greeting with a gold accent, and a thin gold rule underneath.
 */
export function WelcomeBand({ eyebrow, title, accent, children, className }: { eyebrow?: string; title: string; accent?: string; children?: ReactNode; className?: string }) {
  return (
    <section className={cn("brand-panel relative mb-6 overflow-hidden rounded-panel p-6 text-navy-ink sm:p-8", className)}>
      <DriftingCircles density="light" />
      <div className="relative z-10">
        {eyebrow && <p className="text-sm font-medium text-gold">{eyebrow}</p>}
        <h1 className="mt-1 font-display text-3xl font-semibold sm:text-4xl">
          {title}
          {accent && (
            <>
              {" "}
              <em className="text-gold">{accent}</em>
            </>
          )}
        </h1>
        <span className="mt-3 block h-0.5 w-16 rounded-full bg-gold" aria-hidden />
        {children && <div className="mt-4 text-sm text-navy-ink/80">{children}</div>}
      </div>
    </section>
  );
}
