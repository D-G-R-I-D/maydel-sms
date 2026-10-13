import { Card, Skeleton } from "@/components/ui/primitives";

export default function Loading() {
  return (
    <div aria-busy="true" aria-live="polite">
      <span className="sr-only">Loading…</span>
      <div className="mb-6 flex items-end justify-between gap-4">
        <div>
          <Skeleton className="mb-2 h-9 w-56 max-w-full" />
          <Skeleton className="h-4 w-80 max-w-full" />
        </div>
        <Skeleton className="hidden h-11 w-36 sm:block" />
      </div>
      <Card className="divide-y divide-border">
        {Array.from({ length: 6 }, (_, i) => (
          <div key={i} className="flex items-center gap-4 px-5 py-4">
            <Skeleton className="size-10 rounded-full" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-3 w-56 max-w-full" />
            </div>
            <Skeleton className="hidden h-8 w-24 sm:block" />
          </div>
        ))}
      </Card>
    </div>
  );
}
