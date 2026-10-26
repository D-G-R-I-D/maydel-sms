import Link from "next/link";
import type { PortalChild } from "@/lib/api/types";
import { cn } from "@/components/ui/primitives";
import { PhotoAvatar } from "@/components/media/photo-avatar";
import { childAccent } from "@/lib/portal/accent";

/** Pills to switch between a parent's children (shown only with more than one), kept in `?child=`. */
export function ChildSwitcher({ items, activeId, basePath }: { items: PortalChild[]; activeId: string; basePath: string }) {
  if (items.length < 2) return null;
  return (
    <nav aria-label="Choose a child" className="-mt-2 mb-5 flex gap-2 overflow-x-auto pb-1">
      {items.map((c, i) => {
        const active = c.studentId === activeId;
        const accent = childAccent(i);
        return (
          <Link
            key={c.studentId}
            href={`${basePath}?child=${c.studentId}`}
            aria-current={active ? "page" : undefined}
            className={cn(
              "inline-flex min-h-11 shrink-0 items-center gap-2 rounded-full border py-1 pr-4 pl-1 text-sm transition-colors",
              active ? "border-brand bg-brand/10 font-medium text-brand" : "border-border bg-surface text-text hover:bg-surface-2",
            )}
          >
            <PhotoAvatar kind="portal" id={c.studentId} name={c.name} photoVersion={c.photoVersion} className={cn("size-8 text-xs ring-2", accent.ring)} />
            {c.name.split(" ")[0]}
          </Link>
        );
      })}
    </nav>
  );
}
