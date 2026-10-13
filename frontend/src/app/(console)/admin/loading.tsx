import { Card, Skeleton } from "@/components/ui/primitives";

export default function Loading() {
  return (
    <div aria-busy="true" aria-live="polite">
      <span className="sr-only">Loading…</span>
      <Skeleton className="mb-6 h-9 w-64 max-w-full" />
      <div className="grid gap-4 md:grid-cols-2">
        <Card className="h-40 p-5">
          <Skeleton className="h-full" />
        </Card>
        <Card className="h-40 p-5">
          <Skeleton className="h-full" />
        </Card>
      </div>
    </div>
  );
}
