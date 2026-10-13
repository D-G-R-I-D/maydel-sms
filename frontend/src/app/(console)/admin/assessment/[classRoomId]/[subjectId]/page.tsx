import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { formatScore } from "@/lib/assessment/grid";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { ScoreGrid } from "@/components/assessment/score-grid";
import { TermPicker } from "@/components/assessment/term-picker";

export const metadata: Metadata = { title: "Enter scores" };

export default async function GradeSheetPage({
  params,
  searchParams,
}: {
  params: Promise<{ classRoomId: string; subjectId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { classRoomId, subjectId } = await params;
  const sp = await searchParams;
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/assessment" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      My classes
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

  const { terms, selected } = await loadTerms(Array.isArray(sp.termId) ? sp.termId[0] : sp.termId);
  if (!selected) notFound();

  const [sheet, settings] = await Promise.all([
    load((api) => api.GET("/api/assessment/gradesheet", { params: { query: { classRoomId, subjectId, termId: selected.id } } })),
    load((api) => api.GET("/api/assessment/settings")),
  ]);

  if (!sheet.ok) {
    if (sheet.problem.status === 404) notFound();
    return (
      <>
        {back}
        <PageHeader title="Enter scores" actions={<TermPicker terms={terms} value={selected.id} />} />
        <Card>
          {sheet.problem.status === 403 ? (
            <EmptyState title="This class and subject aren't assigned to you">
              {sheet.problem.detail ?? "Ask an administrator to assign you as the subject or form teacher."}
            </EmptyState>
          ) : (
            <ProblemState problem={sheet.problem} />
          )}
        </Card>
      </>
    );
  }

  const s = sheet.data;
  const maxTotal = s.components.reduce((n, c) => n + c.maxScore, 0);

  return (
    <>
      {back}
      <PageHeader
        title={`${s.subject} · ${s.classRoom}`}
        description={`${s.term} · ${s.components.map((c) => `${c.name} ${formatScore(c.maxScore)}`).join(" · ")} = ${formatScore(maxTotal)}`}
        actions={<TermPicker terms={terms} value={selected.id} />}
      />
      {s.components.length === 0 ? (
        <Card>
          <EmptyState title="No active score components">An administrator needs to set them up under Settings → Assessment.</EmptyState>
        </Card>
      ) : s.rows.length === 0 ? (
        <Card>
          <EmptyState icon={Users} title="No students in this class">
            Admit students into {s.classRoom} first (Students → Admit student).
          </EmptyState>
        </Card>
      ) : (
        // Re-key per sheet so switching term starts fresh (and restores that sheet's own backup).
        <ScoreGrid key={`${s.classRoomId}:${s.subjectId}:${s.termId}`} initial={s} bands={settings.ok ? settings.data.bands : []} />
      )}
    </>
  );
}
