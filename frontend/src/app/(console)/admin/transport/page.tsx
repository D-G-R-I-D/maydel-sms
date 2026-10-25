import type { Metadata } from "next";
import Link from "next/link";
import { AlertTriangle, Bus, CheckCircle2, ChevronLeft, ChevronRight, CircleDashed } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { addDays, isDate, longDay, todayWat, watTime } from "@/lib/clock/clock";
import { duration, tripState } from "@/lib/transport/transport";
import { Badge, Card, PageHeader, buttonClasses, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { AutoRefresh } from "@/components/clock/correct-day";
import { CheckAnswers } from "@/components/transport/check-answers";
import { CorrectTripButton } from "@/components/transport/correct-trip";
import { TransportTabs } from "@/components/transport/transport-tabs";

export const metadata: Metadata = { title: "Transport" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E18 admin: every bus and run for a day (Left / Back times, duration) and each bus's checklist. */
export default async function TransportTodayPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Transport" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const today = todayWat();
  const raw = one((await searchParams).date);
  const date = isDate(raw) && raw <= today ? raw : today;
  const board = await load((api) => api.GET("/api/transport/board", { params: { query: { date } } }));
  const dayHref = (d: string) => (d === today ? "/admin/transport" : `/admin/transport?date=${d}`);

  return (
    <>
      <PageHeader title="Transport" description="The school buses: when each run left and came back, and the drivers' daily checklists. Times are Abuja time (WAT)." />
      <TransportTabs active="today" />
      {date === today && <AutoRefresh />}
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <h2 className="min-w-0 flex-1 font-display text-lg font-semibold">{date === today ? `Today, ${longDay(date)}` : longDay(date)}</h2>
        <Link href={dayHref(addDays(date, -1))} className={buttonClasses("secondary", "sm")} aria-label="Previous day">
          <ChevronLeft className="size-4" aria-hidden />
        </Link>
        {date === today ? (
          <span className={cn(buttonClasses("secondary", "sm"), "pointer-events-none opacity-50")} aria-disabled="true">
            <ChevronRight className="size-4" aria-hidden />
            <span className="sr-only">Next day</span>
          </span>
        ) : (
          <Link href={dayHref(addDays(date, 1))} className={buttonClasses("secondary", "sm")} aria-label="Next day">
            <ChevronRight className="size-4" aria-hidden />
          </Link>
        )}
      </div>
      {!board.ok ? (
        <Card>
          <ProblemState problem={board.problem} />
        </Card>
      ) : board.data.buses.length === 0 ? (
        <Card>
          <EmptyState
            icon={Bus}
            title="No buses yet"
            action={
              <Link href="/admin/transport/setup" className={buttonClasses("primary")}>
                Add a bus
              </Link>
            }
          >
            Add the school&apos;s buses and assign their drivers in Setup.
          </EmptyState>
        </Card>
      ) : (
        <div className="grid gap-5 xl:grid-cols-2">
          {board.data.buses.map((b) => (
            <Card key={b.bus.id} className="overflow-hidden">
              <div className="flex flex-wrap items-center gap-2 border-b border-border px-5 py-3">
                <Bus className="size-5 text-brand" aria-hidden />
                <h3 className="font-display text-lg font-semibold">{b.bus.name}</h3>
                {b.bus.plateNumber && <span className="text-sm text-muted">{b.bus.plateNumber}</span>}
                <span className="ml-auto text-sm text-muted">{b.bus.driverName ?? "No driver"}</span>
              </div>
              <table className="w-full text-sm">
                <thead className="text-left text-xs tracking-wider text-muted uppercase">
                  <tr className="border-b border-border">
                    <th className="px-5 py-2 font-medium">Run</th>
                    <th className="px-2 py-2 font-medium">Left</th>
                    <th className="px-2 py-2 font-medium">Back</th>
                    <th className="px-2 py-2 text-right font-medium">Time out</th>
                    <th className="px-3 py-2">
                      <span className="sr-only">Correct</span>
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {b.trips.map((t) => {
                    const s = tripState(t);
                    return (
                      <tr key={t.runId}>
                        <td className="px-5 py-2">
                          <span className="font-medium">{t.run}</span>
                          {t.correction && <span className="block text-xs text-muted">Corrected: {t.correction}</span>}
                        </td>
                        <td className="tabular px-2 py-2">{watTime(t.leftAt)}</td>
                        <td className="tabular px-2 py-2">{s === "out" ? <Badge tone="warning">On the road</Badge> : watTime(t.backAt)}</td>
                        <td className="tabular px-2 py-2 text-right">{duration(t.minutes)}</td>
                        <td className="px-3 py-1 text-right">
                          <CorrectTripButton trip={t} />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
              <div className={cn("border-t border-border px-5 py-3", b.check?.isFlagged && "bg-warning/5")}>
                {b.check ? (
                  <details>
                    <summary className="cursor-pointer text-sm font-medium">
                      {b.check.isFlagged ? (
                        <span className="inline-flex items-center gap-1.5 text-warning">
                          <AlertTriangle className="size-4" aria-hidden />
                          Checklist flagged a problem
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1.5 text-success">
                          <CheckCircle2 className="size-4" aria-hidden />
                          Checklist done
                        </span>
                      )}
                    </summary>
                    <div className="mt-2">
                      <CheckAnswers check={b.check} />
                    </div>
                  </details>
                ) : (
                  <p className="inline-flex items-center gap-1.5 text-sm text-muted">
                    <CircleDashed className="size-4" aria-hidden />
                    Checklist not done yet
                  </p>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}
    </>
  );
}
