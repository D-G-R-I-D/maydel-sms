import type { Metadata } from "next";
import { CalendarDays } from "lucide-react";
import { loadSite } from "@/lib/site/api";
import { EventItem } from "@/components/site/cards";
import { Card } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Events", description: "Upcoming events at Maydel Spring Schools, Abuja." };

/** E7: upcoming public events, soonest first. Times in Abuja time (WAT). */
export default async function EventsPage() {
  const events = await loadSite((api) => api.GET("/api/public/site/events", { params: { query: { take: 50 } } }));
  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <h1 className="font-display text-4xl font-semibold">Events</h1>
      <p className="mt-1 text-muted">What&apos;s coming up at school. All times are Abuja time (WAT).</p>
      <Card className="mt-6 p-5">
        {!events.ok ? (
          <ProblemState problem={events.problem} />
        ) : events.data.length === 0 ? (
          <EmptyState icon={CalendarDays} title="No upcoming events">
            Check back soon.
          </EmptyState>
        ) : (
          <ul className="divide-y divide-border">
            {events.data.map((e) => (
              <li key={e.id} className="py-4 first:pt-0 last:pb-0">
                <EventItem event={e} />
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}
