import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { isFormTeacherOf } from "@/lib/reports/access";
import { formatAverage } from "@/lib/reports/rules";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TermPicker } from "@/components/assessment/term-picker";
import { ClassReports } from "@/components/reports/class-reports";

export const metadata: Metadata = { title: "Class reports" };

function Stat({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="rounded-input bg-surface-2/60 px-3 py-2">
      <p className="text-xs text-muted">{label}</p>
      <p className="tabular font-semibold">{value}</p>
    </div>
  );
}

export default async function ClassReportsPage({
  params,
  searchParams,
}: {
  params: Promise<{ classRoomId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { classRoomId } = await params;
  const sp = await searchParams;
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/reports" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Reports
    </Link>
  );
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        {back}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const { terms, selected } = await loadTerms(Array.isArray(sp.termId) ? sp.termId[0] : sp.termId);
  if (!selected) notFound();

  const [summary, formTeacher, behaviour, emails] = await Promise.all([
    load((api) => api.GET("/api/reports/class", { params: { query: { classRoomId, termId: selected.id } } })),
    isAdmin ? Promise.resolve(true) : isFormTeacherOf(classRoomId),
    load((api) => api.GET("/api/assessment/behaviour")),
    isAdmin ? load((api) => api.GET("/api/reports/emails", { params: { query: { classRoomId, termId: selected.id } } })) : Promise.resolve(null),
  ]);
  const traitCount = behaviour.ok ? behaviour.data.traits.filter((t) => t.isActive).length : null;
  if (!summary.ok) {
    if (summary.problem.status === 404) notFound();
    return (
      <>
        {back}
        <PageHeader title="Class reports" actions={<TermPicker terms={terms} value={selected.id} />} />
        <Card>
          <ProblemState problem={summary.problem} />
        </Card>
      </>
    );
  }

  const s = summary.data;
  // No attendance typed in: reports use the daily register's totals (E9), copied in on publish.
  const register = s.daysOpened === null ? await load((api) => api.GET("/api/attendance/summary", { params: { query: { classRoomId, termId: selected.id } } })) : null;
  const registerDays = register?.ok ? register.data.daysOpened : 0;
  return (
    <>
      {back}
      <PageHeader
        title={`${s.classRoom} reports`}
        description={`${s.term}${s.formTeacher ? ` · Form teacher: ${s.formTeacher}` : ""}`}
        actions={<TermPicker terms={terms} value={selected.id} />}
      />
      <div className="mb-5 grid grid-cols-2 gap-2 sm:grid-cols-4">
        <Stat label="Students" value={s.rows.length} />
        <Stat label="Class average" value={formatAverage(s.rows.length ? s.classAverage : null)} />
        <Stat label="Published / draft" value={`${s.publishedCount} / ${s.draftCount}`} />
        <Stat label="Days school opened" value={s.daysOpened ?? (registerDays > 0 ? `From register: ${registerDays} ${registerDays === 1 ? "day" : "days"}` : "Not entered")} />
      </div>
      {s.rows.length === 0 ? (
        <Card>
          <EmptyState icon={Users} title="No students in this class">
            Admit students into {s.classRoom} first.
          </EmptyState>
        </Card>
      ) : (
        <ClassReports summary={s} isAdmin={isAdmin} canAttendance={formTeacher} traitCount={traitCount} emails={emails} registerDays={registerDays} />
      )}
    </>
  );
}
