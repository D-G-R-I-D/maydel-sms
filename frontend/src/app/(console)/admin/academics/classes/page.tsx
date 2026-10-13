import type { Metadata } from "next";
import Link from "next/link";
import { BookOpen, ChevronRight, Eye, Layers } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { groupBySection } from "@/lib/validation/academics";
import { Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { CapacityBar } from "@/components/academics/capacity-bar";
import { EditClassroomButton, NewClassroomButton } from "@/components/academics/classroom-forms";

export const metadata: Metadata = { title: "Classes" };

export default async function ClassesPage() {
  const user = await requireSessionUser();
  const header = (actions?: React.ReactNode) => (
    <PageHeader title="Classes" description="Every class by section, with its form teacher and how full it is." actions={actions} />
  );
  if (!hasAnyRole(user.roles, ["Admin", "Teacher", "Bursar"])) {
    return (
      <>
        {header()}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const [classrooms, levels] = await Promise.all([
    load((api) => api.GET("/api/academics/classrooms")),
    load((api) => api.GET("/api/academics/class-levels")),
  ]);
  const levelList = levels.ok ? levels.data : [];
  const newButton = isAdmin && levelList.length > 0 ? <NewClassroomButton levels={levelList} /> : undefined;

  if (!classrooms.ok) {
    return (
      <>
        {header()}
        <Card>
          <ProblemState problem={classrooms.problem} />
        </Card>
      </>
    );
  }

  const groups = groupBySection(classrooms.data);
  const totalStudents = classrooms.data.reduce((n, c) => n + c.studentCount, 0);

  return (
    <>
      {header(newButton)}
      {!isAdmin && (
        <p className="-mt-3 mb-5 flex items-center gap-1.5 text-sm text-muted">
          <Eye className="size-4" aria-hidden />
          View only.
        </p>
      )}

      {groups.length === 0 ? (
        <Card>
          <EmptyState icon={Layers} title="No classes yet" action={newButton}>
            {isAdmin ? "Add a class for each year group and arm, e.g. Year 3 A." : "An administrator hasn't set up classes yet."}
          </EmptyState>
        </Card>
      ) : (
        <>
          <p className="tabular mb-4 text-sm text-muted">
            {classrooms.data.length} classes · {totalStudents} students
          </p>
          <div className="space-y-6">
            {groups.map((group) => (
              <section key={group.section} aria-labelledby={`section-${group.section}`} className="animate-rise">
                <h2 id={`section-${group.section}`} className="mb-2 flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-muted">
                  {group.label}
                  <span className="rounded-full bg-surface-2 px-2 py-0.5 text-xs font-medium normal-case tracking-normal">{group.rows.length}</span>
                </h2>
                <Card className="overflow-hidden">
                  {/* Table from sm up, stacked rows on phones. */}
                  <table className="hidden w-full text-left text-sm sm:table">
                    <thead className="bg-surface-2 text-xs uppercase tracking-wide text-muted">
                      <tr>
                        <th scope="col" className="px-4 py-2.5 font-medium">Class</th>
                        <th scope="col" className="px-4 py-2.5 font-medium">Form teacher</th>
                        <th scope="col" className="px-4 py-2.5 font-medium">Students</th>
                        <th scope="col" className="px-4 py-2.5 text-right font-medium"><span className="sr-only">Actions</span></th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-border">
                      {group.rows.map((c) => (
                        <tr key={c.id} className="transition-colors hover:bg-surface-2/50">
                          <th scope="row" className="px-4 py-3 font-medium">
                            <Link href={`/admin/academics/classes/${c.id}`} className="hover:text-brand hover:underline">
                              {c.name}
                            </Link>
                          </th>
                          <td className="px-4 py-3">{c.formTeacher ?? <span className="text-muted">Not assigned</span>}</td>
                          <td className="px-4 py-3">
                            <CapacityBar count={c.studentCount} capacity={c.capacity} />
                          </td>
                          <td className="px-4 py-3">
                            <div className="flex items-center justify-end gap-1">
                              <Link href={`/admin/academics/classes/${c.id}`} className={buttonClasses("ghost", "sm")}>
                                <BookOpen className="size-4" aria-hidden />
                                Subjects
                              </Link>
                              {isAdmin && levels.ok && <EditClassroomButton levels={levelList} classroom={c} />}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <ul className="divide-y divide-border sm:hidden">
                    {group.rows.map((c) => (
                      <li key={c.id} className="flex items-center gap-3 px-4 py-3">
                        <Link href={`/admin/academics/classes/${c.id}`} className="min-w-0 flex-1">
                          <p className="font-medium">{c.name}</p>
                          <p className="truncate text-sm text-muted">{c.formTeacher ?? "No form teacher"}</p>
                          <div className="mt-1.5">
                            <CapacityBar count={c.studentCount} capacity={c.capacity} />
                          </div>
                        </Link>
                        {isAdmin && levels.ok && <EditClassroomButton levels={levelList} classroom={c} />}
                        <ChevronRight className="size-4 text-muted" aria-hidden />
                      </li>
                    ))}
                  </ul>
                </Card>
              </section>
            ))}
          </div>
        </>
      )}
    </>
  );
}
