import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Eye, UserRound, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { sectionLabels } from "@/lib/validation/academics";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { CapacityBar } from "@/components/academics/capacity-bar";
import { ClassSubjectsEditor } from "@/components/academics/class-subjects-editor";
import { EditClassroomButton } from "@/components/academics/classroom-forms";

export const metadata: Metadata = { title: "Class subjects" };

export default async function ClassSubjectsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/academics/classes" className="mb-4 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      All classes
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
  // There's no single-class endpoint yet; the list is small (one row per class).
  const [classrooms, assigned, subjects, levels] = await Promise.all([
    load((api) => api.GET("/api/academics/classrooms")),
    load((api) => api.GET("/api/academics/classrooms/{id}/subjects", { params: { path: { id } } })),
    load((api) => api.GET("/api/academics/subjects")),
    load((api) => api.GET("/api/academics/class-levels")),
  ]);

  if (!assigned.ok && assigned.problem.status === 404) notFound();
  const classroom = classrooms.ok ? classrooms.data.find((c) => c.id === id) : undefined;
  if (classrooms.ok && !classroom) notFound();

  const failed = [classrooms, assigned, subjects].find((r) => !r.ok);
  if (failed && !failed.ok) {
    return (
      <>
        {back}
        <Card>
          <ProblemState problem={failed.problem} />
        </Card>
      </>
    );
  }
  if (!classroom || !assigned.ok || !subjects.ok) return null;

  return (
    <>
      {back}
      <PageHeader
        title={classroom.name}
        description={`${sectionLabels[classroom.section]} · choose the subjects this class offers.`}
        actions={isAdmin && levels.ok ? <EditClassroomButton levels={levels.data} classroom={classroom} /> : undefined}
      />

      <div className="mb-5 grid gap-3 sm:grid-cols-2">
        <Card className="flex items-center gap-3 p-4">
          <span className="inline-flex size-10 items-center justify-center rounded-full bg-brand/10 text-brand">
            <UserRound className="size-5" aria-hidden />
          </span>
          <div>
            <p className="text-xs text-muted">Form teacher</p>
            <p className="font-medium">{classroom.formTeacher ?? "Not assigned"}</p>
          </div>
        </Card>
        <Card className="flex items-center gap-3 p-4">
          <span className="inline-flex size-10 items-center justify-center rounded-full bg-brand/10 text-brand">
            <Users className="size-5" aria-hidden />
          </span>
          <div className="flex-1">
            <p className="text-xs text-muted">Students</p>
            <CapacityBar count={classroom.studentCount} capacity={classroom.capacity} />
          </div>
        </Card>
      </div>

      {!isAdmin && (
        <p className="mb-4 flex items-center gap-1.5 text-sm text-muted">
          <Eye className="size-4" aria-hidden />
          View only.
        </p>
      )}

      <ClassSubjectsEditor classroomId={classroom.id} className={classroom.name} assigned={assigned.data} subjects={subjects.data} canEdit={isAdmin} />
    </>
  );
}
