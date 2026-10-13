import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { StudentForm } from "@/components/students/student-form";

export const metadata: Metadata = { title: "Admit student" };

export default async function AdmitStudentPage() {
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/students" className="mb-4 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      All students
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

  const classrooms = await load((api) => api.GET("/api/academics/classrooms"));
  return (
    <>
      {back}
      <PageHeader title="Admit student" description="The admission number is generated when you save. Add guardians and a login on the next screen." />
      <Card className="p-5 sm:p-6">
        {classrooms.ok ? <StudentForm classrooms={classrooms.data} /> : <ProblemState problem={classrooms.problem} />}
      </Card>
    </>
  );
}
