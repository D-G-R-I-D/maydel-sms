import Image from "next/image";
import Link from "next/link";
import { MapPin } from "lucide-react";
import { siteImage, type NewsSummary, type PublicEvent } from "@/lib/site/api";
import { dateTile, formatDay, formatEventWhen } from "@/lib/site/format";
import { Badge, Card } from "@/components/ui/primitives";

/** A news card for lists and the homepage. */
export function NewsCard({ item }: { item: NewsSummary }) {
  const img = siteImage(item.imageUrl);
  return (
    <Card className="group flex h-full flex-col overflow-hidden">
      <Link href={`/news/${item.slug}`} className="flex h-full flex-col">
        <div className="relative aspect-[16/9] bg-surface-2">
          {img ? (
            <Image src={img} alt="" fill sizes="(min-width: 1024px) 360px, (min-width: 640px) 50vw, 100vw" className="object-cover" />
          ) : (
            <div className="brand-panel flex h-full items-center justify-center" aria-hidden>
              <Image src="/brand/crest.svg" alt="" width={56} height={70} className="opacity-90" />
            </div>
          )}
        </div>
        <div className="flex flex-1 flex-col gap-2 p-4">
          <p className="flex flex-wrap items-center gap-2 text-xs text-muted">
            {item.isPinned && <Badge tone="brand">Pinned</Badge>}
            {item.category && <span>{item.category}</span>}
            <time dateTime={item.publishedAt}>{formatDay(item.publishedAt)}</time>
          </p>
          <h3 className="font-display text-lg font-semibold group-hover:text-brand">{item.title}</h3>
          {item.summary && <p className="line-clamp-3 text-sm text-muted">{item.summary}</p>}
        </div>
      </Link>
    </Card>
  );
}

/** An event row with a date tile. */
export function EventItem({ event }: { event: PublicEvent }) {
  const tile = dateTile(event.startsAt);
  return (
    <div className="flex gap-4">
      <div className="flex size-14 shrink-0 flex-col items-center justify-center rounded-input bg-brand/10 text-brand" aria-hidden>
        <span className="text-[0.625rem] font-semibold tracking-wider">{tile.month}</span>
        <span className="font-display text-xl leading-none font-semibold">{tile.day}</span>
      </div>
      <div className="min-w-0">
        <h3 className="font-semibold">{event.title}</h3>
        <p className="text-sm text-muted">
          <time dateTime={event.startsAt}>{formatEventWhen(event.startsAt, event.endsAt)}</time>
        </p>
        {event.location && (
          <p className="flex items-center gap-1 text-sm text-muted">
            <MapPin className="size-3.5" aria-hidden />
            {event.location}
          </p>
        )}
        {event.description && <p className="mt-1 text-sm whitespace-pre-line">{event.description}</p>}
      </div>
    </div>
  );
}

/** Section heading used across the site. */
export function SectionHeading({ eyebrow, title, children }: { eyebrow?: string; title: string; children?: React.ReactNode }) {
  return (
    <div className="mb-6 max-w-2xl">
      {eyebrow && <p className="text-sm font-semibold tracking-wide text-brand uppercase">{eyebrow}</p>}
      <h2 className="font-display text-2xl font-semibold sm:text-3xl">{title}</h2>
      {children && <p className="mt-2 text-muted">{children}</p>}
    </div>
  );
}
