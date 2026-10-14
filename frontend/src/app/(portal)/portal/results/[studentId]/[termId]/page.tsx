import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeft, FileSearch } from "lucide-react";
import { load } from "@/lib/api/server";
import { buttonClasses, Card } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { ReportCardView } from "@/components/reports/report-card";
import { PdfDownloadButton } from "@/components/reports/pdf-download";
import { ReadOnlyBehaviour, ReadOnlyComments } from "@/components/portal/read-only";

export const metadata: Metadata = { title: "Report card" };

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** A published report card for the parent's/student's own child: read-only, phone-first, PDF download. */
export default async function PortalReportPage({ params }: { params: Promise<{ studentId: string; termId: string }> }) {
  const { studentId, termId } = await params;
  const back = (
    <Link href={`/portal/results?child=${studentId}`} className="mb-3 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Results
    </Link>
  );
  const notFound = (
    <>
      {back}
      <Card>
        <EmptyState
          icon={FileSearch}
          title="This report isn't available"
          action={
            <Link href="/portal/results" className={buttonClasses("secondary")}>
              Back to results
            </Link>
          }
        >
          It may not be published yet, or it isn&apos;t one of your children&apos;s reports.
        </EmptyState>
      </Card>
    </>
  );
  if (!GUID.test(studentId) || !GUID.test(termId)) return notFound;

  const card = await load((api) => api.GET("/api/portal/children/{studentId}/results/{termId}", { params: { path: { studentId, termId } } }));
  if (!card.ok) {
    if (card.problem.status === 404) return notFound;
    return (
      <>
        {back}
        <Card>
          <ProblemState problem={card.problem} />
        </Card>
      </>
    );
  }
  const c = card.data;

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-2">
        {back}
        <PdfDownloadButton
          href={`/api/media/portal/children/${studentId}/results/${termId}/pdf`}
          label="Download PDF"
          size="sm"
          fallbackName={`${c.studentName} - ${c.term} ${c.session}.pdf`}
        />
      </div>
      <ReportCardView card={c} photoKind="portal" summaryFirst comments={<ReadOnlyComments card={c} />} extra={<ReadOnlyBehaviour card={c} />} />
    </>
  );
}
