import type { Metadata } from "next";
import { Eye } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { NewSubjectButton, SubjectsTable } from "@/components/academics/subjects-table";

export const metadata: Metadata = { title: "Subjects" };

export default async function SubjectsPage() {
  const user = await requireSessionUser();
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const header = (
    <PageHeader title="Subjects" description="The subject catalogue. Choose which classes offer each one under Classes." actions={isAdmin ? <NewSubjectButton /> : undefined} />
  );
  if (!hasAnyRole(user.roles, ["Admin", "Teacher", "Bursar"])) {
    return (
      <>
        {header}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  const subjects = await load((api) => api.GET("/api/academics/subjects"));
  return (
    <>
      {header}
      {!isAdmin && (
        <p className="-mt-3 mb-5 flex items-center gap-1.5 text-sm text-muted">
          <Eye className="size-4" aria-hidden />
          View only.
        </p>
      )}
      {subjects.ok ? (
        <SubjectsTable subjects={subjects.data} canEdit={isAdmin} />
      ) : (
        <Card>
          <ProblemState problem={subjects.problem} />
        </Card>
      )}
    </>
  );
}
