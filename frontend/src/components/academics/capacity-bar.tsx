import { cn } from "@/components/ui/primitives";

// Literal class names so Tailwind generates them (no inline styles).
const widths = ["w-0", "w-1/12", "w-2/12", "w-3/12", "w-4/12", "w-5/12", "w-6/12", "w-7/12", "w-8/12", "w-9/12", "w-10/12", "w-11/12", "w-full"];

/** Students / capacity, as text plus a bar. Full classes get a warning colour and the word "Full". */
export function CapacityBar({ count, capacity }: { count: number; capacity: number }) {
  const ratio = capacity > 0 ? Math.min(1, count / capacity) : 0;
  const width = widths[Math.round(ratio * 12)];
  const full = count >= capacity;
  return (
    <div className="min-w-28">
      <p className="tabular text-sm">
        {count} <span className="text-muted">/ {capacity}</span>
        {full && <span className="ml-1.5 text-xs font-medium text-warning">Full</span>}
      </p>
      <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-surface-2" aria-hidden>
        <div className={cn("h-full rounded-full", width, full ? "bg-warning" : ratio > 0.85 ? "bg-accent-strong" : "bg-brand")} />
      </div>
    </div>
  );
}
