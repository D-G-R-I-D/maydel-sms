import Image from "next/image";
import { cn } from "./primitives";

type Size = "xs" | "sm" | "md" | "lg";

const box: Record<Size, string> = { xs: "size-4", sm: "size-6", md: "size-12", lg: "size-20" };
const crest: Record<Size, number> = { xs: 10, sm: 14, md: 28, lg: 46 };

/**
 * The school's loader: the crest breathing gently inside a thin rotating gold arc. Calm, not flashy.
 * Under prefers-reduced-motion the arc is hidden and only a soft opacity pulse remains (globals.css).
 * Decorative by itself; wrap it (LoaderStatus / button text) for the accessible label.
 */
export function SchoolLoader({ size = "md", className }: { size?: Size; className?: string }) {
  return (
    <span aria-hidden className={cn("school-loader relative inline-flex shrink-0 items-center justify-center", box[size], className)}>
      <svg className="school-loader-arc absolute inset-0 size-full" viewBox="0 0 48 48" fill="none">
        <circle cx="24" cy="24" r="21" stroke="currentColor" strokeOpacity="0.14" strokeWidth={size === "xs" ? 4 : 2.5} />
        <circle cx="24" cy="24" r="21" stroke="#d9b16a" strokeWidth={size === "xs" ? 4 : 2.5} strokeLinecap="round" strokeDasharray="34 98" />
      </svg>
      <span className="school-loader-crest inline-flex items-center justify-center rounded-full bg-white/95 p-[12%] shadow-sm">
        <Image src="/brand/crest.svg" alt="" width={crest[size]} height={Math.round(crest[size] * 1.25)} priority={size === "lg"} />
      </span>
    </span>
  );
}

/** Loader + visible/hidden label as a polite live status, for whole-region waits. */
export function LoaderStatus({ label = "Loading…", size = "md", showLabel = true, className }: { label?: string; size?: Size; showLabel?: boolean; className?: string }) {
  return (
    <div role="status" aria-live="polite" className={cn("inline-flex flex-col items-center gap-3", className)}>
      <SchoolLoader size={size} />
      <span className={showLabel ? "text-sm text-muted" : "sr-only"}>{label}</span>
    </div>
  );
}

/**
 * Full-page route loading (loading.tsx): centred crest + label, revealed only after ~200 ms so fast
 * navigations don't flash. The region is aria-busy while shown.
 */
export function RouteLoader({ label = "Loading…" }: { label?: string }) {
  return (
    <div aria-busy="true" className="loader-delayed flex min-h-[50dvh] w-full items-center justify-center py-16">
      <LoaderStatus label={label} size="lg" />
    </div>
  );
}
