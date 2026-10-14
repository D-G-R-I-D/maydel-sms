import Image from "next/image";
import { cn } from "./primitives";

type Size = "xs" | "sm" | "md" | "lg";

const box: Record<Size, string> = { xs: "size-4", sm: "size-6", md: "size-14", lg: "size-24" };
const crest: Record<Size, number> = { xs: 10, sm: 14, md: 30, lg: 52 };

/**
 * The school's loader, the one loading indicator used across the app:
 * - the crest breathing gently (scale + opacity);
 * - a gold "comet" ring sweeping around it, bright at the head and fading at the tail;
 * - (md/lg) a soft gold halo that ripples outward.
 * Only transform/opacity animate (GPU-friendly, no layout shift). Under prefers-reduced-motion
 * the ring and halo stop and the crest just pulses its opacity (globals.css).
 * Decorative by itself; LoaderStatus / button text supply the accessible label.
 */
export function SchoolLoader({ size = "md", className }: { size?: Size; className?: string }) {
  const big = size === "md" || size === "lg";
  return (
    <span aria-hidden className={cn("school-loader relative inline-flex shrink-0 items-center justify-center", !big && "school-loader--small", box[size], className)}>
      {big && <span className="school-loader-halo absolute inset-0 rounded-full" />}
      <span className="school-loader-track absolute inset-0 rounded-full" />
      <span className="school-loader-ring absolute inset-0 rounded-full" />
      <span className="school-loader-crest relative inline-flex items-center justify-center rounded-full bg-white p-[12%] shadow-sm">
        <Image src="/brand/crest.svg" alt="" width={crest[size]} height={Math.round(crest[size] * 1.25)} priority={size === "lg"} />
      </span>
    </span>
  );
}

/** "Loading" with three softly pulsing dots (opacity only). */
function LoadingLabel({ label }: { label: string }) {
  const text = label.replace(/[.…]+$/, "");
  return (
    <span className="inline-flex items-baseline text-sm font-medium tracking-wide text-muted">
      {text}
      <span className="loader-dots ml-0.5 inline-flex" aria-hidden>
        <span>.</span>
        <span>.</span>
        <span>.</span>
      </span>
    </span>
  );
}

/** Loader + label as a polite live status, for whole-region waits. */
export function LoaderStatus({ label = "Loading…", size = "md", showLabel = true, className }: { label?: string; size?: Size; showLabel?: boolean; className?: string }) {
  return (
    <div role="status" aria-live="polite" className={cn("inline-flex flex-col items-center gap-4", className)}>
      <SchoolLoader size={size} />
      {showLabel ? (
        <>
          <LoadingLabel label={label} />
          <span className="sr-only">{label}</span>
        </>
      ) : (
        <span className="sr-only">{label}</span>
      )}
    </div>
  );
}

/**
 * Full-page route loading (loading.tsx): the crest centred in the content area with its label,
 * revealed after ~200 ms so fast navigations don't flash, fading in gently.
 */
export function RouteLoader({ label = "Loading…" }: { label?: string }) {
  return (
    <div aria-busy="true" className="loader-delayed grid min-h-[50dvh] w-full place-items-center px-4 py-16">
      <LoaderStatus label={label} size="lg" />
    </div>
  );
}
