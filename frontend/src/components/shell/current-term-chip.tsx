import Link from "next/link";
import { CalendarDays, CalendarOff } from "lucide-react";
import { load } from "@/lib/api/server";
import { cn } from "@/components/ui/primitives";

/**
 * The current term in the header ("First Term 2026/2027"). Streams in behind <Suspense>; a 404
 * `term.none_current` (or any failure) shows a quiet "No current term" rather than breaking the shell.
 */
export async function CurrentTermChip({ href }: { href?: string }) {
  const term = await load((api) => api.GET("/api/academics/terms/current"));
  const label = term.ok ? term.data.name : "No current term";
  const Icon = term.ok ? CalendarDays : CalendarOff;
  const classes = cn(
    "animate-fade inline-flex min-h-8 max-w-[14rem] items-center gap-1.5 rounded-full px-3 text-xs font-medium",
    term.ok ? "bg-brand/10 text-brand" : "bg-surface-2 text-muted",
  );
  const body = (
    <>
      <Icon className="size-3.5 shrink-0" aria-hidden />
      <span className="truncate">{label}</span>
    </>
  );
  return href ? (
    <Link href={href} className={cn(classes, "hover:bg-brand/15")} title={term.ok ? "Current term" : "No term is marked current"}>
      {body}
    </Link>
  ) : (
    <span className={classes} title={term.ok ? "Current term" : "No term is marked current"}>
      {body}
    </span>
  );
}

export function CurrentTermChipFallback() {
  return <span className="inline-block h-8 w-36 animate-pulse rounded-full bg-surface-2" aria-hidden />;
}
