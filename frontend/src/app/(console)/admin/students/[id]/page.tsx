import type { Metadata } from "next";
import { PdfDownloadButton } from "@/components/reports/pdf-download";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Eye, HeartPulse, Layers } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { formatDay } from "@/lib/validation/academics";
import { ageOn, statusTone } from "@/lib/validation/students";
import { Badge, Card, cn } from "@/components/ui/primitives";
import { AccessDenied, Alert, ProblemState } from "@/components/ui/states";
import { PhotoControls } from "@/components/media/photo-controls";
import { IssueLoginButton } from "@/components/credentials/issue-login";
import { EditStudentButton } from "@/components/students/edit-student";
import { GuardiansSection } from "@/components/students/guardians-section";
import { StatusMenu } from "@/components/students/status-menu";
import { StudentStatementView } from "@/components/finance/student-statement";

export const metadata: Metadata = { title: "Student" };

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[9rem_1fr] gap-3 py-2 text-sm">
      <dt className="text-muted">{label}</dt>
      <dd>{value ?? <span className="text-muted">—</span>}</dd>
    </div>
  );
}

export default async function StudentProfilePage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { id } = await params;
  const sp = await searchParams;
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/students" className="mb-4 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      All students
    </Link>
  );
  if (!hasAnyRole(user.roles, ["Admin", "Teacher", "Bursar"])) {
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
  const canSeeFees = hasAnyRole(user.roles, ["Admin", "Bursar"]);
  const tab = canSeeFees && (Array.isArray(sp.tab) ? sp.tab[0] : sp.tab) === "fees" ? "fees" : "profile";
  const [result, classrooms, statement] = await Promise.all([
    load((api) => api.GET("/api/students/{id}", { params: { path: { id } } })),
    isAdmin ? load((api) => api.GET("/api/academics/classrooms")) : Promise.resolve(null),
    tab === "fees" ? load((api) => api.GET("/api/finance/students/{id}/statement", { params: { path: { id } } })) : Promise.resolve(null),
  ]);
  if (!result.ok && result.problem.status === 404) notFound();
  if (!result.ok) {
    return (
      <>
        {back}
        <Card>
          <ProblemState problem={result.problem} />
        </Card>
      </>
    );
  }

  const s = result.data;
  const active = s.status === "Active";

  return (
    <>
      {back}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center">
        <PhotoControls kind="students" id={s.id} name={s.fullName} photoVersion={s.photoVersion} canEdit={isAdmin} />
        <div className="min-w-0 flex-1">
          <h1 className="flex flex-wrap items-center gap-2 font-display text-2xl font-semibold sm:text-3xl">
            {s.fullName}
            <Badge tone={statusTone[s.status]}>{s.status}</Badge>
          </h1>
          <p className="flex flex-wrap items-center gap-x-3 text-sm text-muted">
            <span className="font-mono">{s.admissionNumber}</span>
            {s.classRoomId ? (
              <Link href={`/admin/academics/classes/${s.classRoomId}`} className="inline-flex items-center gap-1 text-brand hover:underline">
                <Layers className="size-3.5" aria-hidden />
                {s.classRoom}
              </Link>
            ) : (
              <span>No class</span>
            )}
            {!s.hasLogin && <Badge tone="warning">No login</Badge>}
          </p>
        </div>
        {isAdmin && (
          <div className="flex flex-wrap gap-2">
            {active && <IssueLoginButton kind="student" id={s.id} fullName={s.fullName} description={`${s.admissionNumber}${s.classRoom ? ` · ${s.classRoom}` : ""}`} hasLogin={s.hasLogin} />}
            <StatusMenu studentId={s.id} fullName={s.fullName} status={s.status} />
            {classrooms?.ok && <EditStudentButton student={s} classrooms={classrooms.data} />}
            <PdfDownloadButton href={`/api/media/students/${s.id}/id-card`} label="Print ID card" fallbackName={`${s.admissionNumber.replace(/\//g, "-")}-id-card.pdf`} icon="print" size="sm" />
          </div>
        )}
      </div>

      {!isAdmin && (
        <p className="mb-4 flex items-center gap-1.5 text-sm text-muted">
          <Eye className="size-4" aria-hidden />
          View only.
        </p>
      )}
      {!active && (
        <Alert tone="warning" className="mb-4" title={`This student is ${s.status.toLowerCase()}`}>
          Their login is disabled and they don&apos;t appear in their parents&apos; portal. Records are kept.
        </Alert>
      )}
      {s.medicalNotes && (
        <Alert tone="danger" className="mb-4" title="Medical notes">
          <p className="flex items-start gap-2 whitespace-pre-line">
            <HeartPulse className="mt-0.5 size-4 shrink-0" aria-hidden />
            {s.medicalNotes}
          </p>
        </Alert>
      )}

      {canSeeFees && (
        <nav aria-label="Student sections" className="mb-5 inline-flex rounded-input bg-surface-2 p-1">
          {(
            [
              ["profile", "Profile", `/admin/students/${s.id}`],
              ["fees", "Fees", `/admin/students/${s.id}?tab=fees`],
            ] as const
          ).map(([key, label, href]) => (
            <Link
              key={key}
              href={href}
              aria-current={tab === key ? "page" : undefined}
              className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", tab === key ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
            >
              {label}
            </Link>
          ))}
        </nav>
      )}

      {tab === "fees" ? (
        statement?.ok ? (
          <StudentStatementView statement={statement.data} />
        ) : (
          <Card>{statement && !statement.ok && <ProblemState problem={statement.problem} />}</Card>
        )
      ) : (
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
        <Card className="p-5">
          <h2 className="mb-2 font-display text-lg font-semibold">Biodata</h2>
          <dl className="divide-y divide-border">
            <Row label="First name" value={s.firstName} />
            <Row label="Middle name" value={s.middleName} />
            <Row label="Surname" value={s.lastName} />
            <Row label="Date of birth" value={s.dateOfBirth ? `${formatDay(s.dateOfBirth)} (age ${ageOn(s.dateOfBirth)})` : null} />
            <Row label="Sex" value={s.sex} />
            <Row label="State of origin" value={s.stateOfOrigin} />
            <Row label="LGA" value={s.lga} />
            <Row label="House" value={s.house} />
            <Row label="Home address" value={s.homeAddress} />
            <Row label="Admitted on" value={formatDay(s.admittedOn)} />
          </dl>
        </Card>
        <GuardiansSection studentId={s.id} studentName={s.fullName} guardians={s.guardians} canEdit={isAdmin} />
      </div>
      )}
    </>
  );
}
