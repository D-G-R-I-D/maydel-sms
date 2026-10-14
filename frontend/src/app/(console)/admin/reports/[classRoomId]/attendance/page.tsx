import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { isFormTeacherOf } from "@/lib/reports/access";
import { Card, PageHeader } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { AttendanceForm } from "@/components/reports/attendance-form";

export const metadata: Metadata = { title: "Attendance" };

export default async function AttendancePage({
  params,
  searchParams,
}: {
  params: Promise<{ classRoomId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { classRoomId } = await params;
  const sp = await searchParams;
  const user = await requireSessionUser();
  const { selected } = await loadTerms(Array.isArray(sp.termId) ? sp.termId[0] : sp.termId);
  if (!selected) notFound();
  const back = (
    <Link href={`/admin/reports/${classRoomId}?termId=${selected.id}`} className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Class reports
    </Link>
  );

  const allowed = hasAnyRole(user.roles, ["Admin"]) || (hasAnyRole(user.roles, ["Teacher"]) && (await isFormTeacherOf(classRoomId)));
  if (!allowed) {
    return (
      <>
        {back}
        <Card>
          <EmptyState title="Only the form teacher or an admin enters attendance">Ask the class&apos;s form teacher to enter it.</EmptyState>
        </Card>
      </>
    );
  }

  const summary = await load((api) => api.GET("/api/reports/class", { params: { query: { classRoomId, termId: selected.id } } }));
  if (!summary.ok) {
    if (summary.problem.status === 404) notFound();
    return (
      <>
        {back}
        <Card>
          <ProblemState problem={summary.problem} />
        </Card>
      </>
    );
  }
  return (
    <>
      {back}
      <PageHeader
        title={`Attendance · ${summary.data.classRoom}`}
        description={`${summary.data.term}. Entered by hand until the daily register arrives; it appears on each report card.`}
      />
      <AttendanceForm summary={summary.data} />
    </>
  );
}
