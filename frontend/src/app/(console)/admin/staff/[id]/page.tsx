import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, BookOpen, Clock, Hash, Layers, Mail, Phone, UserCheck } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { formatLastSeen, isSelf } from "@/lib/validation/staff";
import { Avatar, Badge, Card } from "@/components/ui/primitives";
import { AccessDenied, Alert, ProblemState } from "@/components/ui/states";
import { StaffForm } from "@/components/staff/staff-form";
import { IssueLoginButton } from "@/components/credentials/issue-login";

export const metadata: Metadata = { title: "Staff member" };

export default async function StaffDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/staff" className="mb-4 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      All staff
    </Link>
  );
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        {back}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  const result = await load((api) => api.GET("/api/staff/{id}", { params: { path: { id } } }));
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

  const staff = result.data;
  const self = isSelf(staff.email, user.userName);
  const formClasses = staff.assignments.filter((a) => a.subjectId === null);
  const subjects = staff.assignments.filter((a) => a.subjectId !== null);

  return (
    <>
      {back}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center">
        <Avatar name={staff.fullName} className="size-14 text-lg" />
        <div className="min-w-0 flex-1">
          <h1 className="flex flex-wrap items-center gap-2 font-display text-2xl font-semibold sm:text-3xl">
            {staff.fullName}
            {self && <Badge tone="brand">You</Badge>}
            {!staff.isActive && <Badge>Inactive</Badge>}
          </h1>
          <p className="text-sm text-muted">{staff.jobTitle ?? "Staff"}</p>
        </div>
        {staff.hasLogin && staff.isActive && !self && (
          <IssueLoginButton kind="staff" id={staff.id} hasLogin fullName={staff.fullName} description={staff.jobTitle ?? "Staff"} />
        )}
      </div>

      {staff.mustChangePassword && staff.hasLogin && (
        <Alert tone="info" className="mb-4">
          They haven&apos;t chosen their own password yet. They&apos;ll be asked to when they first sign in.
        </Alert>
      )}

      <div className="grid gap-5 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <Card className="p-5">
          <h2 className="mb-4 font-display text-lg font-semibold">Details</h2>
          <StaffForm mode="edit" staff={staff} self={self} />
        </Card>

        <div className="space-y-5">
          <Card className="p-5">
            <h2 className="mb-3 font-display text-lg font-semibold">Account</h2>
            <dl className="space-y-2.5 text-sm">
              <div className="flex items-center gap-2">
                <Hash className="size-4 text-muted" aria-hidden />
                <dt className="sr-only">Staff number</dt>
                <dd className="font-mono">{staff.staffNumber}</dd>
              </div>
              <div className="flex items-center gap-2">
                <Mail className="size-4 text-muted" aria-hidden />
                <dt className="sr-only">Email (username)</dt>
                <dd className="break-all">{staff.email}</dd>
              </div>
              {staff.phone && (
                <div className="flex items-center gap-2">
                  <Phone className="size-4 text-muted" aria-hidden />
                  <dt className="sr-only">Phone</dt>
                  <dd>
                    <a href={`tel:${staff.phone.replace(/[^\d+]/g, "")}`} className="text-brand hover:underline">
                      {staff.phone}
                    </a>
                  </dd>
                </div>
              )}
              <div className="flex items-center gap-2">
                <UserCheck className="size-4 text-muted" aria-hidden />
                <dt className="sr-only">Login</dt>
                <dd>{staff.hasLogin ? "Has a login" : "No login"}</dd>
              </div>
              <div className="flex items-center gap-2">
                <Clock className="size-4 text-muted" aria-hidden />
                <dt className="sr-only">Last sign-in</dt>
                <dd>Last sign-in: {formatLastSeen(staff.lastLoginAt)}</dd>
              </div>
            </dl>
            {self && <p className="mt-3 text-xs text-muted">To change your own password, use Change password in your account menu.</p>}
          </Card>

          <Card className="p-5">
            <h2 className="mb-3 font-display text-lg font-semibold">Teaching</h2>
            {staff.assignments.length === 0 ? (
              <p className="text-sm text-muted">No classes or subjects yet. Assign them under Academics → Classes.</p>
            ) : (
              <div className="space-y-4">
                {formClasses.length > 0 && (
                  <div>
                    <p className="mb-1.5 text-xs font-semibold uppercase tracking-wider text-muted">Form teacher of</p>
                    <ul className="flex flex-wrap gap-2">
                      {formClasses.map((a) => (
                        <li key={a.classRoomId}>
                          <Link href={`/admin/academics/classes/${a.classRoomId}`} className="inline-flex min-h-9 items-center gap-1.5 rounded-full bg-brand/10 px-3 text-sm text-brand hover:bg-brand/15">
                            <Layers className="size-3.5" aria-hidden />
                            {a.classRoom}
                          </Link>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                {subjects.length > 0 && (
                  <div>
                    <p className="mb-1.5 text-xs font-semibold uppercase tracking-wider text-muted">Teaches</p>
                    <ul className="divide-y divide-border text-sm">
                      {subjects.map((a) => (
                        <li key={`${a.classRoomId}-${a.subjectId}`}>
                          <Link href={`/admin/academics/classes/${a.classRoomId}`} className="flex min-h-10 items-center gap-2 hover:text-brand">
                            <BookOpen className="size-3.5 text-muted" aria-hidden />
                            <span className="flex-1">{a.subject}</span>
                            <span className="text-muted">{a.classRoom}</span>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            )}
          </Card>
        </div>
      </div>
    </>
  );
}
