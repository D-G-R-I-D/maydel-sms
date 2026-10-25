import type { Metadata } from "next";
import { Bus, Download } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { addDays, isDate, longDay, shortDay, todayWat, watTime } from "@/lib/clock/clock";
import { duration } from "@/lib/transport/transport";
import { Button, Card, Input, PageHeader, Select, buttonClasses, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TransportTabs } from "@/components/transport/transport-tabs";

export const metadata: Metadata = { title: "Transport reports" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E18 admin: every trip in a date range (optionally one bus), with CSV export. */
export default async function TransportReportsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
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
  const sp = await searchParams;
  const today = todayWat();
  const to = isDate(one(sp.to)) ? one(sp.to)! : today;
  const from = isDate(one(sp.from)) ? one(sp.from)! : addDays(to, -13);
  const busId = GUID.test(one(sp.busId) ?? "") ? one(sp.busId) : undefined;
  const [buses, trips] = await Promise.all([
    load((api) => api.GET("/api/transport/buses")),
    from <= to ? load((api) => api.GET("/api/transport/trips", { params: { query: { from, to, ...(busId ? { busId } : {}) } } })) : null,
  ]);
  const csv = `/api/media/transport/trips-csv?from=${from}&to=${to}${busId ? `&busId=${busId}` : ""}`;
  const rows = trips?.ok ? trips.data.filter((t) => t.leftAt || t.backAt) : [];

  return (
    <>
      <PageHeader title="Transport" description="Trips over a date range. Times are Abuja time (WAT)." />
      <TransportTabs active="reports" />
      <Card className="overflow-hidden">
        <form className="flex flex-wrap items-end gap-3 border-b border-border p-5" action="/admin/transport/reports">
          <div className="space-y-1.5 text-sm">
            <label htmlFor="tr-from" className="block font-medium">
              From
            </label>
            <Input id="tr-from" type="date" name="from" defaultValue={from} max={today} />
          </div>
          <div className="space-y-1.5 text-sm">
            <label htmlFor="tr-to" className="block font-medium">
              To
            </label>
            <Input id="tr-to" type="date" name="to" defaultValue={to} max={today} />
          </div>
          <div className="space-y-1.5 text-sm">
            <label htmlFor="tr-bus" className="block font-medium">
              Bus
            </label>
            <Select id="tr-bus" name="busId" defaultValue={busId ?? ""}>
              <option value="">All buses</option>
              {buses.ok &&
                buses.data.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
            </Select>
          </div>
          <Button type="submit" variant="secondary">
            Show
          </Button>
          {rows.length > 0 && (
            <a href={csv} className={cn(buttonClasses("secondary"), "ml-auto")} download>
              <Download className="size-4" aria-hidden />
              CSV
            </a>
          )}
        </form>
        {!trips ? (
          <p className="p-5 text-sm text-danger" role="alert">
            The start date must be on or before the end date.
          </p>
        ) : !trips.ok ? (
          <ProblemState problem={trips.problem} />
        ) : rows.length === 0 ? (
          <EmptyState icon={Bus} title="No trips in this range" />
        ) : (
          <>
            <p className="border-b border-border px-5 py-3 text-sm text-muted">
              {longDay(from)} to {longDay(to)} · <span className="tabular">{rows.length}</span> trips
            </p>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[42rem] text-sm">
                <thead className="text-left text-xs tracking-wider text-muted uppercase">
                  <tr className="border-b border-border">
                    <th className="px-5 py-2.5 font-medium">Day</th>
                    <th className="px-3 py-2.5 font-medium">Bus</th>
                    <th className="px-3 py-2.5 font-medium">Run</th>
                    <th className="px-3 py-2.5 font-medium">Left</th>
                    <th className="px-3 py-2.5 font-medium">Back</th>
                    <th className="px-5 py-2.5 text-right font-medium">Time out</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {rows.map((t) => (
                    <tr key={`${t.date}-${t.busId}-${t.runId}`}>
                      <td className="px-5 py-2.5 whitespace-nowrap">{shortDay(t.date)}</td>
                      <td className="px-3 py-2.5">{t.bus}</td>
                      <td className="px-3 py-2.5">
                        {t.run}
                        {t.correction && <span className="block text-xs text-muted">Corrected: {t.correction}</span>}
                      </td>
                      <td className="tabular px-3 py-2.5">{watTime(t.leftAt)}</td>
                      <td className="tabular px-3 py-2.5">{watTime(t.backAt)}</td>
                      <td className="tabular px-5 py-2.5 text-right">{duration(t.minutes)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </Card>
    </>
  );
}
