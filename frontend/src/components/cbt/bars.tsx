import { cn } from "@/components/ui/primitives";

// Literal class names so Tailwind generates them (no inline styles); 12 steps is plenty for a chart.
const widths = ["w-0", "w-1/12", "w-2/12", "w-3/12", "w-4/12", "w-5/12", "w-6/12", "w-7/12", "w-8/12", "w-9/12", "w-10/12", "w-11/12", "w-full"];
const heights = ["h-0", "h-1/12", "h-2/12", "h-3/12", "h-4/12", "h-5/12", "h-6/12", "h-7/12", "h-8/12", "h-9/12", "h-10/12", "h-11/12", "h-full"];
const step = (ratio: number) => Math.round(Math.max(0, Math.min(1, ratio)) * 12);

const tones = { brand: "bg-brand", success: "bg-success", warning: "bg-warning", danger: "bg-danger", neutral: "bg-muted/50" } as const;
export type BarTone = keyof typeof tones;

/** A horizontal bar for a 0–1 ratio; the number beside it is the accessible value. */
export function HBar({ ratio, tone = "brand", className }: { ratio: number; tone?: BarTone; className?: string }) {
  return (
    <div className={cn("h-2 overflow-hidden rounded-full bg-surface-2", className)} aria-hidden>
      <div className={cn("h-full rounded-full", widths[step(ratio)], tones[tone])} />
    </div>
  );
}

/** Column chart of counts (e.g. the 10 score bands), with a visually hidden table for screen readers. */
export function Columns({ data, caption }: { data: { label: string; value: number }[]; caption: string }) {
  const max = Math.max(1, ...data.map((d) => d.value));
  return (
    <figure>
      <div className="flex h-40 items-end gap-1" aria-hidden>
        {data.map((d) => (
          <div key={d.label} className="flex h-full min-w-0 flex-1 flex-col items-center justify-end gap-1">
            <span className="tabular text-xs text-muted">{d.value > 0 ? d.value : ""}</span>
            <div className={cn("w-full rounded-t-[4px] bg-brand", heights[step(d.value / max)], d.value > 0 && "min-h-1")} />
          </div>
        ))}
      </div>
      <div className="mt-1 flex gap-1 text-[0.625rem] text-muted" aria-hidden>
        {data.map((d) => (
          <span key={d.label} className="min-w-0 flex-1 truncate text-center">
            {d.label}
          </span>
        ))}
      </div>
      <table className="sr-only">
        <caption>{caption}</caption>
        <tbody>
          {data.map((d) => (
            <tr key={d.label}>
              <th scope="row">{d.label}</th>
              <td>{d.value}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  );
}
