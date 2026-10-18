import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, CheckCircle2, Lock } from "lucide-react";
import { load } from "@/lib/api/server";
import { GUID } from "@/lib/media/binary-proxy";
import { applicationStatusLabel, applicationStatusTone } from "@/lib/admissions/rules";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { Alert, ProblemState } from "@/components/ui/states";
import { PdfDownloadButton } from "@/components/reports/pdf-download";
import { ApplicationFormEditor } from "@/components/admissions/application-form-editor";
import { ApplicationFormView } from "@/components/admissions/application-form-view";
import { DocumentsPanel } from "@/components/admissions/documents-panel";
import { NextSteps } from "@/components/admissions/next-steps";

export const metadata: Metadata = { title: "Application" };

const timeFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "long", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });

/** E11.5: one application: the form (draft → submit), documents, then the next steps. */
export default async function ApplicationPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!GUID.test(id)) notFound();
  const detail = await load((api) => api.GET("/api/admissions/applications/{id}", { params: { path: { id } } }));
  const back = (
    <Link href="/admissions" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Your applications
    </Link>
  );
  if (!detail.ok) {
    if (detail.problem.status === 404) notFound();
    return (
      <>
        {back}
        <Card>
          <ProblemState problem={detail.problem} />
        </Card>
      </>
    );
  }
  const d = detail.data;
  const a = d.application;
  // Exam times to book or move to, while that's still possible (E11.6).
  const slots =
    a.status === "Submitted" || a.status === "ExamBooked"
      ? await load((api) => api.GET("/api/admissions/applications/{id}/exam-slots", { params: { path: { id } } }))
      : null;
  const pdf = (
    <PdfDownloadButton
      href={`/api/media/admissions/applications/${a.id}/form-pdf`}
      label={d.submittedAt ? "Download form (PDF)" : "Preview PDF"}
      fallbackName={`${a.referenceNumber} - ${a.childFirstName} ${a.childLastName}.pdf`}
      size="sm"
    />
  );

  return (
    <>
      {back}
      <PageHeader
        title={`${a.childFirstName} ${a.childLastName}`}
        description={
          <>
            {a.classLevel} · {a.session} · <span className="tabular">{a.referenceNumber}</span>
          </>
        }
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={applicationStatusTone[a.status]}>{applicationStatusLabel[a.status]}</Badge>
            {a.status !== "AwaitingPayment" && pdf}
          </div>
        }
      />

      {a.status === "AwaitingPayment" ? (
        <Card className="p-5">
          <p className="flex items-center gap-2 text-sm">
            <Lock className="size-4 text-muted" aria-hidden />
            Pay for the form first (online or with a form code) on <Link href="/admissions" className="font-medium text-brand hover:underline">your applications</Link> page.
          </p>
        </Card>
      ) : d.canEdit ? (
        <div className="space-y-5">
          <Alert tone="info" title="Fill in the form, add the passport photograph, then submit">
            Save a draft any time and come back later. You can&apos;t make changes after submitting.
          </Alert>
          <DocumentsPanel detail={d} />
          <ApplicationFormEditor detail={d} mode="family" />
        </div>
      ) : (
        <div className="space-y-5">
          <NextSteps detail={d} slots={slots?.ok ? slots.data : []} />
          {d.submittedAt && (
            <Alert tone="success" title={`Submitted on ${timeFormat.format(new Date(d.submittedAt))}`}>
              <span className="flex items-center gap-1.5">
                <CheckCircle2 className="size-4" aria-hidden />
                The form is locked.
              </span>
            </Alert>
          )}
          <ApplicationFormView detail={d} />
          <DocumentsPanel detail={d} />
        </div>
      )}
    </>
  );
}
