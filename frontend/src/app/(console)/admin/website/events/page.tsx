import type { Metadata } from "next";
import Link from "next/link";
import { CalendarDays, MapPin, Pencil, Plus } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { addDays, todayWat } from "@/lib/clock/clock";
import { dateTile, formatEventWhen } from "@/lib/site/format";
import { audienceLabel, splitEvents } from "@/lib/website/posts";
import { Badge, Button, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { DeleteEvent, EventDialog } from "@/components/website/event-dialog";

export const metadata: Metadata = { title: "Events" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E7 console: school events, upcoming first; public ones appear on the website. */
export default async function EventsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Events" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const past = one((await searchParams).view) === "past";
  const today = todayWat();
  // Upcoming: from today. Past: the last year, newest first.
  const list = await load((api) => api.GET("/api/cms/events", { params: { query: { from: past ? `${addDays(today, -365)}T00:00:00+01:00` : `${today}T00:00:00+01:00` } } }));
  const items = list.ok ? splitEvents(list.data, past) : [];
  const add = (
    <EventDialog
      item={null}
      trigger={
        <Button>
          <Plus className="size-4" aria-hidden />
          Add event
        </Button>
      }
    />
  );

  return (
    <>
      <PageHeader title="Events" description="Open days, sports days, celebrations and meetings. Public events show on the website." actions={add} />
      <nav aria-label="Event range" className="mb-5 inline-flex rounded-input bg-surface-2 p-1">
        {(["upcoming", "past"] as const).map((v) => (
          <Link
            key={v}
            href={v === "past" ? "/admin/website/events?view=past" : "/admin/website/events"}
            aria-current={(v === "past") === past ? "page" : undefined}
            className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", (v === "past") === past ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
          >
            {v === "past" ? "Past" : "Upcoming"}
          </Link>
        ))}
      </nav>
      {!list.ok ? (
        <Card>
          <ProblemState problem={list.problem} />
        </Card>
      ) : items.length === 0 ? (
        <Card>
          <EmptyState icon={CalendarDays} title={past ? "No past events this year" : "No upcoming events"} action={past ? undefined : add}>
            {past ? undefined : "Add the next open day, sports day or meeting."}
          </EmptyState>
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <ul className="divide-y divide-border">
            {items.map((e) => {
              const tile = dateTile(e.startsAt);
              return (
                <li key={e.id} className="flex items-center gap-4 px-5 py-3">
                  <span className="flex w-14 shrink-0 flex-col items-center rounded-input bg-brand/10 py-1.5 text-brand">
                    <span className="text-[0.6875rem] font-semibold tracking-wider">{tile.month}</span>
                    <span className="tabular font-display text-xl leading-none font-semibold">{tile.day}</span>
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium">{e.title}</span>
                    <span className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted">
                      <span>{formatEventWhen(e.startsAt, e.endsAt)}</span>
                      {e.location && (
                        <span className="inline-flex items-center gap-1">
                          <MapPin className="size-3" aria-hidden />
                          {e.location}
                        </span>
                      )}
                      <Badge tone={e.audience.includes("Public") || e.audience.includes("Everyone") ? "success" : "neutral"}>{audienceLabel(e.audience)}</Badge>
                    </span>
                  </span>
                  <span className="flex shrink-0">
                    <EventDialog
                      item={e}
                      trigger={
                        <Button variant="ghost" size="sm" aria-label={`Edit ${e.title}`}>
                          <Pencil className="size-4" aria-hidden />
                        </Button>
                      }
                    />
                    <DeleteEvent item={e} />
                  </span>
                </li>
              );
            })}
          </ul>
        </Card>
      )}
    </>
  );
}
