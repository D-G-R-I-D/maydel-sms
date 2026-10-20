import Link from "next/link";
import { Megaphone } from "lucide-react";

/**
 * E7.9 news ticker: a slow right-to-left scroll that pauses on hover or focus; under
 * prefers-reduced-motion it's a plain wrapping list. The duplicate run is hidden from screen readers.
 */
export function NewsTicker({ items }: { items: { id: string; text: string; href: string | null }[] }) {
  if (items.length === 0) return null;
  const run = (hidden: boolean) => (
    <ul className="flex shrink-0 items-center gap-8 pr-8" aria-hidden={hidden || undefined}>
      {items.map((t) => (
        <li key={t.id} className="whitespace-nowrap">
          {t.href ? (
            <Link href={t.href} tabIndex={hidden ? -1 : undefined} className="hover:underline">
              {t.text}
            </Link>
          ) : (
            t.text
          )}
        </li>
      ))}
    </ul>
  );
  return (
    <section aria-label="Latest announcements" className="border-y border-border bg-brand text-sm text-on-brand">
      <div className="mx-auto flex max-w-6xl items-center gap-3 px-4 sm:px-6">
        <span className="flex shrink-0 items-center gap-1.5 py-2 font-semibold">
          <Megaphone className="size-4" aria-hidden />
          News
        </span>
        <div className="ticker group relative min-w-0 flex-1 overflow-hidden py-2">
          <div className="ticker-track flex w-max group-hover:[animation-play-state:paused] group-focus-within:[animation-play-state:paused]">
            {run(false)}
            {run(true)}
          </div>
        </div>
      </div>
    </section>
  );
}
