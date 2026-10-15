import type { Metadata } from "next";
import Link from "next/link";
import { CalendarCheck } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { attendanceTone, formatRegisterDate, isIsoDate } from "@/lib/attendance/rules";
import { schoolToday } from "@/lib/validation/students";
import { Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TermPicker } from "@/components/assessment/term-picker";
import { RegisterControls } from "@/components/attendance/register-controls";
import { RegisterEditor } from "@/components/attendance/register-editor";

export const metadata: Metadata = { title: "Attendance" };

type SP = Record<string, string | string[] | undefined>;
const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

/** E9.1–E9.2: the daily class register (teachers land on their form class, today) and term summaries. */
export default async function AttendancePage({ searchParams }: { searchParams: Promise<SP> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        <PageHeader title="Attendance" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const sp = await searchParams;
  const tab = one(sp.tab) === "summary" ? "summary" : "register";
  const today = schoolToday();
  const date = isIsoDate(one(sp.date)) && one(sp.date)! <= today ? one(sp.date)! : today;

  // Admins: every class (including ones without subjects yet). Teachers: classes they teach, form class first.
  const [mine, all] = await Promise.all([
    load((api) => api.GET("/api/assessment/my-classes")),
    isAdmin ? load((api) => api.GET("/api/academics/classrooms")) : Promise.resolve(null),
  ]);
  const failed = !mine.ok ? mine.problem : all && !all.ok ? all.problem : null;
  if (failed) {
    return (
      <>
        <PageHeader title="Attendance" />
        <Card>
          <ProblemState problem={failed} />
        </Card>
      </>
    );
  }
  const byId = new Map<string, { id: string; name: string; order: number; isForm: boolean }>();
  if (all?.ok) for (const c of all.data) byId.set(c.id, { id: c.id, name: c.name, order: c.levelOrder, isForm: false });
  if (mine.ok)
    for (const c of mine.data) {
      const g = byId.get(c.classRoomId) ?? { id: c.classRoomId, name: c.classRoom, order: c.levelOrder, isForm: false };
      g.isForm ||= c.isFormClass;
      byId.set(c.classRoomId, g);
    }
  const classes = [...byId.values()].sort((a, b) => Number(b.isForm) - Number(a.isForm) || a.order - b.order || a.name.localeCompare(b.name));
  if (classes.length === 0) {
    return (
      <>
        <PageHeader title="Attendance" />
        <Card>
          <EmptyState icon={CalendarCheck} title="No classes yet">
            {isAdmin ? "Create classes under Academics first." : "You'll see your form class here once an administrator assigns you to one."}
          </EmptyState>
        </Card>
      </>
    );
  }
  const requested = one(sp.classRoomId);
  const current = classes.find((c) => c.id === requested) ?? classes[0];

  const tabHref = (t: "register" | "summary") => {
    const q = new URLSearchParams({ classRoomId: current.id });
    if (t === "summary") q.set("tab", "summary");
    else if (date !== today) q.set("date", date);
    return `/admin/attendance?${q.toString()}`;
  };
  const nav = (
    <nav aria-label="Attendance views" className="mb-5 inline-flex rounded-input bg-surface-2 p-1">
      {(["register", "summary"] as const).map((t) => (
        <Link
          key={t}
          href={tabHref(t)}
          aria-current={tab === t ? "page" : undefined}
          className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", tab === t ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
        >
          {t === "register" ? "Daily register" : "Term summary"}
        </Link>
      ))}
    </nav>
  );

  if (tab === "summary") {
    const { terms, selected } = await loadTerms(one(sp.termId));
    const summary = selected ? await load((api) => api.GET("/api/attendance/summary", { params: { query: { classRoomId: current.id, termId: selected.id } } })) : null;
    return (
      <>
        <PageHeader
          title={`${current.name} attendance`}
          description="Days present this term, from the daily register."
          actions={selected ? <TermPicker terms={terms} value={selected.id} /> : undefined}
        />
        {nav}
        {!summary ? (
          <Card>
            <EmptyState title="No terms yet">Set up the school calendar first.</EmptyState>
          </Card>
        ) : !summary.ok ? (
          <Card>
            <ProblemState problem={summary.problem} />
          </Card>
        ) : summary.data.rows.length === 0 || summary.data.daysOpened === 0 ? (
          <Card>
            <EmptyState icon={CalendarCheck} title="No registers taken this term">
              Totals appear here once the daily register has been taken.
            </EmptyState>
          </Card>
        ) : (
          <Card className="overflow-x-auto">
            <p className="border-b border-border px-4 py-3 text-sm text-muted">
              Register taken on <span className="tabular font-medium text-text">{summary.data.daysOpened}</span> {summary.data.daysOpened === 1 ? "day" : "days"}. Below 90% is amber, below 75% red.
            </p>
            <table className="w-full min-w-[34rem] text-sm">
              <thead className="text-left text-xs uppercase tracking-wider text-muted">
                <tr>
                  <th className="px-4 py-2 font-medium">Student</th>
                  <th className="px-2 py-2 text-right font-medium">Present</th>
                  <th className="px-2 py-2 text-right font-medium">Absent</th>
                  <th className="px-2 py-2 text-right font-medium">Late</th>
                  <th className="px-2 py-2 text-right font-medium">Excused</th>
                  <th className="px-4 py-2 text-right font-medium">%</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {summary.data.rows.map((r) => {
                  const tone = attendanceTone(r.percentage);
                  return (
                    <tr key={r.studentId}>
                      <td className="px-4 py-2">
                        <span className="font-medium">{r.name}</span>
                        <span className="block text-xs text-muted">{r.admissionNumber}</span>
                      </td>
                      <td className="tabular px-2 py-2 text-right">{r.present}</td>
                      <td className="tabular px-2 py-2 text-right">{r.absent}</td>
                      <td className="tabular px-2 py-2 text-right">{r.late}</td>
                      <td className="tabular px-2 py-2 text-right">{r.excused}</td>
                      <td className="px-4 py-2 text-right">
                        <span
                          className={cn(
                            "tabular inline-flex min-w-14 justify-end rounded-full px-2 py-0.5 font-medium",
                            tone === "danger" && "bg-danger/12 text-danger",
                            tone === "warning" && "bg-warning/15 text-warning",
                          )}
                        >
                          {r.percentage === null ? "—" : `${r.percentage.toFixed(1)}%`}
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </Card>
        )}
      </>
    );
  }

  const register = await load((api) => api.GET("/api/attendance/register", { params: { query: { classRoomId: current.id, date } } }));
  const readOnlyHint = !register.ok
    ? null
    : register.data.canEdit || register.data.problem
      ? null
      : !isAdmin && !current.isForm
        ? "Only the form teacher or an administrator can take this class's register."
        : date < today
          ? "Only an administrator can change earlier days."
          : "This register can't be changed.";

  return (
    <>
      <PageHeader
        title={`${current.name} register`}
        description={formatRegisterDate(date)}
        actions={<RegisterControls classes={classes.map(({ id, name, isForm }) => ({ id, name, isForm }))} classRoomId={current.id} date={date} today={today} />}
      />
      {nav}
      {register.ok ? (
        <RegisterEditor key={`${current.id}:${date}`} initial={register.data} readOnlyHint={readOnlyHint} />
      ) : (
        <Card>
          <ProblemState problem={register.problem} />
        </Card>
      )}
    </>
  );
}
