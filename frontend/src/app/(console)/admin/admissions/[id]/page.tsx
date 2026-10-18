import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Mail, Phone } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { applicationStatusLabel, applicationStatusTone } from "@/lib/admissions/rules";
import { formatNaira } from "@/lib/finance/rules";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, Alert, ProblemState } from "@/components/ui/states";
import { PdfDownloadButton } from "@/components/reports/pdf-download";
import { ApplicationFormView } from "@/components/admissions/application-form-view";
import { DocumentsPanel } from "@/components/admissions/documents-panel";
import { CorrectFormButton } from "@/components/admissions/correct-form-button";
import { EnrolButton, ReopenButton } from "@/components/admissions/staff-application-actions";
import { formatSlotRange, modeLabel } from "@/lib/admissions/exam";

export const metadata: Metadata = { title: "Application" };

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });
const day = (iso: string) => dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`));
const timeFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "long", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });

/** E11: one application for staff: the family's form and documents, and (admin) corrections. */
export default async function StaffApplicationPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/admissions" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Applications
    </Link>
  );
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        {back}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  if (!GUID.test(id)) notFound();
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const detail = await load((api) => api.GET("/api/admissions/applications/{id}/detail", { params: { path: { id } } }));
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
  const submitted = !["AwaitingPayment", "FormPaid"].includes(a.status);
  const correctable = isAdmin && submitted && !["Enrolled", "Withdrawn"].includes(a.status);
  const hasLetter = ["Offered", "Accepted", "Enrolled"].includes(a.status);
  const childName = `${a.childFirstName} ${a.childLastName}`;
  // Class arms at the applied-for level, for enrolment (E11.8).
  const classrooms =
    isAdmin && a.status === "Accepted"
      ? await load((api) => api.GET("/api/academics/classrooms")).then((r) =>
          r.ok ? r.data.filter((c) => c.classLevelId === a.classLevelId).map((c) => ({ id: c.id, name: c.name, full: c.studentCount >= c.capacity })) : [],
        )
      : [];

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
            {a.status !== "AwaitingPayment" && (
              <PdfDownloadButton href={`/api/media/admissions/staff/applications/${a.id}/form-pdf`} label="Form PDF" fallbackName={`${a.referenceNumber}.pdf`} size="sm" />
            )}
            {hasLetter && (
              <PdfDownloadButton href={`/api/media/admissions/staff/applications/${a.id}/letter`} label="Admission letter" fallbackName={`Admission letter - ${childName}.pdf`} size="sm" />
            )}
            {correctable && <CorrectFormButton detail={d} />}
            {isAdmin && a.status === "Missed" && <ReopenButton applicationId={a.id} childName={childName} />}
            {isAdmin && a.status === "Accepted" && <EnrolButton applicationId={a.id} childName={childName} classrooms={classrooms} />}
            {a.status === "Enrolled" && a.studentId && (
              <Link href={`/admin/students/${a.studentId}`} className="text-sm text-brand hover:underline">
                Student record{a.admissionNumber ? ` (${a.admissionNumber})` : ""}
              </Link>
            )}
          </div>
        }
      />

      <Card className="mb-5 grid gap-4 p-5 text-sm sm:grid-cols-3">
        <div>
          <p className="text-muted">Parent</p>
          <p className="font-medium">{a.parentName}</p>
          <a href={`mailto:${a.email}`} className="flex items-center gap-1 text-brand break-all hover:underline">
            <Mail className="size-3.5 shrink-0" aria-hidden />
            {a.email}
          </a>
          <a href={`tel:${a.phone.replace(/[^0-9+]/g, "")}`} className="flex items-center gap-1 text-brand hover:underline">
            <Phone className="size-3.5 shrink-0" aria-hidden />
            {a.phone}
          </a>
        </div>
        <div>
          <p className="text-muted">Form</p>
          <p>
            {a.formPaidAt ? (
              <>
                Paid {day(a.formPaidAt)} ({a.formPaymentMethod === "SchoolCode" ? "form code" : "online"}, {formatNaira(a.formFee)})
              </>
            ) : (
              "Not paid yet"
            )}
          </p>
          <p>{d.submittedAt ? `Submitted ${timeFormat.format(new Date(d.submittedAt))}` : "Not submitted yet"}</p>
        </div>
        <div>
          <p className="text-muted">Entrance exam</p>
          {a.exam ? (
            <p>
              {formatSlotRange(a.exam.startsAt, a.exam.endsAt)} · {a.exam.venue} · {modeLabel[a.exam.mode]}
            </p>
          ) : (
            <p>{a.examWindowEndsOn ? `Book by ${day(a.examWindowEndsOn)}` : "—"}</p>
          )}
          {a.admissionNumber && <p>Admission number <span className="tabular font-mono font-medium">{a.admissionNumber}</span></p>}
          {d.examScore !== null && d.examScore !== undefined && <p className="tabular font-medium">Score {d.examScore}</p>}
        </div>
      </Card>

      {!submitted ? (
        <Alert tone="info" title="Not submitted yet" className="mb-5">
          The family is still filling in the form. You&apos;ll see it here once they submit.
        </Alert>
      ) : (
        <div className="space-y-5">
          <ApplicationFormView detail={d} />
          <DocumentsPanel detail={d} staff />
        </div>
      )}
    </>
  );
}
