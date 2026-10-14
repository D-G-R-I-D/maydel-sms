import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, ChevronLeft, ChevronRight, Lock } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { isFormTeacherOf } from "@/lib/reports/access";
import { neighbours, reportPdfHref } from "@/lib/reports/rules";
import { Badge, Card, buttonClasses, cn } from "@/components/ui/primitives";
import { Alert, ProblemState } from "@/components/ui/states";
import { CommentEditor } from "@/components/reports/comment-editor";
import { ReportCardView } from "@/components/reports/report-card";
import { PdfDownloadButton } from "@/components/reports/pdf-download";
import { BehaviourCard } from "@/components/reports/behaviour-card";

export const metadata: Metadata = { title: "Report card" };

export default async function ReportPreviewPage({
  params,
  searchParams,
}: {
  params: Promise<{ classRoomId: string; studentId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { classRoomId, studentId } = await params;
  const sp = await searchParams;
  const user = await requireSessionUser();
  const { selected } = await loadTerms(Array.isArray(sp.termId) ? sp.termId[0] : sp.termId);
  if (!selected) notFound();
  const termId = selected.id;
  const listHref = `/admin/reports/${classRoomId}?termId=${termId}`;

  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const [card, summary, formTeacher, behaviourSettings] = await Promise.all([
    load((api) => api.GET("/api/reports/{studentId}/{termId}", { params: { path: { studentId, termId } } })),
    load((api) => api.GET("/api/reports/class", { params: { query: { classRoomId, termId } } })),
    isAdmin ? Promise.resolve(true) : isFormTeacherOf(classRoomId),
    load((api) => api.GET("/api/assessment/behaviour")),
  ]);

  const back = (
    <Link href={listHref} className="inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Class reports
    </Link>
  );

  if (!card.ok) {
    if (card.problem.status === 404) notFound();
    return (
      <>
        {back}
        <Card className="mt-2">
          <ProblemState problem={card.problem} />
        </Card>
      </>
    );
  }

  const c = card.data;
  const published = c.status === "Published";
  const nav = summary.ok ? neighbours(summary.data.rows, studentId) : null;
  const navHref = (id: string) => `/admin/reports/${classRoomId}/${id}?termId=${termId}`;

  return (
    <>
      <div className="mb-3 flex flex-wrap items-center gap-2">
        {back}
        <span className="flex-1" />
        <PdfDownloadButton href={reportPdfHref(studentId, termId)} label="Download PDF" size="sm" fallbackName={`${c.studentName} - ${c.term} ${c.session}.pdf`} />
        {published ? (
          <Badge tone="success">
            <Lock className="size-3" aria-hidden />
            Published
          </Badge>
        ) : (
          <Badge tone="warning">Draft</Badge>
        )}
        {nav && (
          <nav aria-label="Other students" className="flex items-center gap-1">
            {nav.prev ? (
              <Link href={navHref(nav.prev.studentId)} className={buttonClasses("secondary", "sm")} aria-label={`Previous: ${nav.prev.name}`}>
                <ChevronLeft className="size-4" aria-hidden />
                <span className="hidden sm:inline">{nav.prev.name}</span>
              </Link>
            ) : (
              <span className={cn(buttonClasses("secondary", "sm"), "pointer-events-none opacity-40")} aria-hidden>
                <ChevronLeft className="size-4" />
              </span>
            )}
            <span className="tabular px-1 text-xs text-muted">
              {nav.index + 1}/{summary.ok ? summary.data.rows.length : "?"}
            </span>
            {nav.next ? (
              <Link href={navHref(nav.next.studentId)} className={buttonClasses("secondary", "sm")} aria-label={`Next: ${nav.next.name}`}>
                <span className="hidden sm:inline">{nav.next.name}</span>
                <ChevronRight className="size-4" aria-hidden />
              </Link>
            ) : (
              <span className={cn(buttonClasses("secondary", "sm"), "pointer-events-none opacity-40")} aria-hidden>
                <ChevronRight className="size-4" />
              </span>
            )}
          </nav>
        )}
      </div>

      {published && (formTeacher || isAdmin) && (
        <Alert tone="info" className="mb-4">
          This report is published: scores, comments and attendance are locked. {isAdmin ? "Unpublish it from Class reports to make corrections." : "Ask an admin to unpublish it."}
        </Alert>
      )}

      <ReportCardView
        card={c}
        comments={
          <>
            {/* Re-key per student so prev/next starts each editor fresh. */}
            <CommentEditor key={`ft-${studentId}`} kind="formTeacher" studentId={studentId} termId={termId} initial={c.formTeacherComment} canEdit={formTeacher} published={published} author={c.formTeacher} />
            <CommentEditor key={`p-${studentId}`} kind="principal" studentId={studentId} termId={termId} initial={c.principalComment} canEdit={isAdmin} published={published} />
          </>
        }
      />
      <BehaviourCard
        key={`behaviour-${studentId}`}
        studentId={studentId}
        termId={termId}
        behaviour={c.behaviour}
        clubs={c.clubs}
        ratingKey={c.behaviourKey}
        maxClubs={behaviourSettings.ok ? behaviourSettings.data.maxClubs : 6}
        canEdit={formTeacher}
        published={published}
      />
      <p className="mt-3 text-xs text-muted">
        The PDF matches what parents will download once the report is published.{!published && " Drafts carry a “DRAFT – NOT FINAL” mark."}
      </p>
    </>
  );
}
