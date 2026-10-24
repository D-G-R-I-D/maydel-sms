import type { Metadata } from "next";
import Link from "next/link";
import { ChevronLeft, ChevronRight, Download, Settings, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { addDays, hhmm, hours, isDate, longDay, stateLabel, stateTone, todayWat, watTime } from "@/lib/clock/clock";
import { Badge, Button, Card, Input, PageHeader, buttonClasses, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { AutoRefresh, CorrectDayButton } from "@/components/clock/correct-day";

export const metadata: Metadata = { title: "Staff attendance" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** Staff clock-in (admin): today's board with corrections, and a report for any date range with CSV. */
export default async function StaffAttendancePage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Staff attendance" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const view = one(sp.view) === "report" ? "report" : "day";
  const today = todayWat();

  const tabs = (
    <nav aria-label="Staff attendance views" className="mb-5 inline-flex rounded-input bg-surface-2 p-1">
      {(["day", "report"] as const).map((t) => (
        <Link
          key={t}
          href={t === "day" ? "/admin/staff/attendance" : "/admin/staff/attendance?view=report"}
          aria-current={view === t ? "page" : undefined}
          className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", view === t ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
        >
          {t === "day" ? "Daily board" : "Report"}
        </Link>
      ))}
    </nav>
  );
  const header = (
    <PageHeader
      title="Staff attendance"
      description="From the kiosk QR at the entrance. Times are Abuja time (WAT)."
      actions={
        <Link href="/admin/settings/clock" className={buttonClasses("secondary", "sm")}>
          <Settings className="size-4" aria-hidden />
          Clock-in settings
        </Link>
      }
    />
  );

  if (view === "report") {
    const to = isDate(one(sp.to)) ? one(sp.to)! : today;
    const from = isDate(one(sp.from)) ? one(sp.from)! : addDays(to, -29);
    const report = from <= to ? await load((api) => api.GET("/api/clock/report", { params: { query: { from, to } } })) : null;
    const csv = `/api/media/clock/report-csv?from=${from}&to=${to}`;
    return (
      <>
        {header}
        {tabs}
        <Card className="overflow-hidden">
          <form className="flex flex-wrap items-end gap-3 border-b border-border p-5" action="/admin/staff/attendance">
            <input type="hidden" name="view" value="report" />
            <label className="space-y-1.5 text-sm">
              <span className="block font-medium">From</span>
              <Input type="date" name="from" defaultValue={from} max={today} />
            </label>
            <label className="space-y-1.5 text-sm">
              <span className="block font-medium">To</span>
              <Input type="date" name="to" defaultValue={to} max={today} />
            </label>
            <Button type="submit" variant="secondary">
              Show
            </Button>
            {report?.ok && report.data.staff.length > 0 && (
              <a href={csv} className={cn(buttonClasses("secondary"), "ml-auto")} download>
                <Download className="size-4" aria-hidden />
                CSV
              </a>
            )}
          </form>
          {!report ? (
            <p className="p-5 text-sm text-danger" role="alert">
              The start date must be on or before the end date.
            </p>
          ) : !report.ok ? (
            <ProblemState problem={report.problem} />
          ) : report.data.staff.length === 0 ? (
            <EmptyState icon={Users} title="No active staff" />
          ) : (
            <>
              <p className="border-b border-border px-5 py-3 text-sm text-muted">
                {longDay(report.data.from)} to {longDay(report.data.to)} · <span className="tabular">{report.data.schoolDays}</span> school days
              </p>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[44rem] text-sm">
                  <thead className="text-left text-xs tracking-wider text-muted uppercase">
                    <tr className="border-b border-border">
                      <th className="px-5 py-2.5 font-medium">Staff</th>
                      <th className="px-3 py-2.5 text-right font-medium">Present</th>
                      <th className="px-3 py-2.5 text-right font-medium">Late</th>
                      <th className="px-3 py-2.5 text-right font-medium">Absent</th>
                      <th className="px-3 py-2.5 text-right font-medium">Avg. arrival</th>
                      <th className="px-5 py-2.5 text-right font-medium">Hours</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {report.data.staff.map((s) => (
                      <tr key={s.staffId}>
                        <td className="px-5 py-2.5">
                          <Link href={`/admin/staff/${s.staffId}`} className="font-medium hover:text-brand hover:underline">
                            {s.name}
                          </Link>
                          {s.jobTitle && <span className="block text-xs text-muted">{s.jobTitle}</span>}
                        </td>
                        <td className="tabular px-3 py-2.5 text-right">
                          {s.daysPresent}
                          <span className="text-muted"> / {s.schoolDays}</span>
                        </td>
                        <td className={cn("tabular px-3 py-2.5 text-right", s.daysLate > 0 && "text-warning")}>{s.daysLate}</td>
                        <td className={cn("tabular px-3 py-2.5 text-right", s.daysAbsent > 0 && "text-danger")}>{s.daysAbsent}</td>
                        <td className="tabular px-3 py-2.5 text-right">{hhmm(s.averageArrival)}</td>
                        <td className="tabular px-5 py-2.5 text-right">{hours(s.hoursWorked)}</td>
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

  const date = isDate(one(sp.date)) && one(sp.date)! <= today ? one(sp.date)! : today;
  const board = await load((api) => api.GET("/api/clock/board", { params: { query: { date } } }));
  const dayHref = (d: string) => (d === today ? "/admin/staff/attendance" : `/admin/staff/attendance?date=${d}`);

  return (
    <>
      {header}
      {tabs}
      {date === today && <AutoRefresh />}
      <Card className="overflow-hidden">
        <div className="flex flex-wrap items-center gap-3 border-b border-border p-5">
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
          <ProblemState problem={board.problem} />
        ) : !board.data.schoolDay ? (
          <EmptyState title="Not a school day">Weekends aren&apos;t counted. Pick a weekday to see who came in.</EmptyState>
        ) : board.data.staff.length === 0 ? (
          <EmptyState icon={Users} title="No active staff" />
        ) : (
          <>
            <dl className="grid grid-cols-3 divide-x divide-border border-b border-border text-center">
              {[
                { label: "Present", value: board.data.present, tone: "text-success" },
                { label: "Late", value: board.data.late, tone: "text-warning" },
                { label: "Absent", value: board.data.absent, tone: "text-danger" },
              ].map((s) => (
                <div key={s.label} className="px-3 py-4">
                  <dt className="text-xs tracking-wider text-muted uppercase">{s.label}</dt>
                  <dd className={cn("tabular font-display text-3xl font-semibold", s.tone)}>{s.value}</dd>
                </div>
              ))}
            </dl>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[40rem] text-sm">
                <thead className="text-left text-xs tracking-wider text-muted uppercase">
                  <tr className="border-b border-border">
                    <th className="px-5 py-2.5 font-medium">Staff</th>
                    <th className="px-3 py-2.5 font-medium">In</th>
                    <th className="px-3 py-2.5 font-medium">Out</th>
                    <th className="px-3 py-2.5 text-right font-medium">Hours</th>
                    <th className="px-3 py-2.5 font-medium">Status</th>
                    <th className="px-5 py-2.5">
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {board.data.staff.map((d) => (
                    <tr key={d.staffId}>
                      <td className="px-5 py-2.5">
                        <span className="font-medium">{d.name}</span>
                        {d.jobTitle && <span className="block text-xs text-muted">{d.jobTitle}</span>}
                      </td>
                      <td className="tabular px-3 py-2.5">
                        {watTime(d.clockInAt)}
                        {d.inMethod === "Manual" && <span className="ml-1 text-xs text-muted">(edited)</span>}
                      </td>
                      <td className="tabular px-3 py-2.5">
                        {watTime(d.clockOutAt)}
                        {d.outMethod === "Manual" && <span className="ml-1 text-xs text-muted">(edited)</span>}
                      </td>
                      <td className="tabular px-3 py-2.5 text-right">{hours(d.hoursWorked)}</td>
                      <td className="px-3 py-2.5">
                        <Badge tone={stateTone[d.state]}>{d.isLate && d.state === "Out" ? "Late, out" : stateLabel[d.state]}</Badge>
                        {d.note && <span className="mt-0.5 block max-w-56 truncate text-xs text-muted" title={d.note}>{d.note}</span>}
                      </td>
                      <td className="px-5 py-2.5 text-right">
                        <CorrectDayButton day={d} />
                      </td>
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
