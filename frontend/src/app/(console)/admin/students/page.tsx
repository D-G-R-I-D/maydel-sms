import type { Metadata } from "next";
import Link from "next/link";
import { ChevronRight, GraduationCap, UserPlus } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { parseStudentSearch, statusTone, studentsHref } from "@/lib/validation/students";
import { Avatar, Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { StudentFilters } from "@/components/students/student-filters";
import { BatchLoginsButton } from "@/components/students/batch-logins";

export const metadata: Metadata = { title: "Students" };

const PAGE_SIZE = 25;

export default async function StudentsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const admit = isAdmin ? (
    <Link href="/admin/students/new" className={buttonClasses("primary")}>
      <UserPlus className="size-4" aria-hidden />
      Admit student
    </Link>
  ) : undefined;

  if (!hasAnyRole(user.roles, ["Admin", "Teacher", "Bursar"])) {
    return (
      <>
        <PageHeader title="Students" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  const search = parseStudentSearch(await searchParams);
  const [students, classrooms] = await Promise.all([
    load((api) =>
      api.GET("/api/students", {
        params: {
          query: {
            search: search.search,
            classRoomId: search.classRoomId,
            status: search.status === "all" ? undefined : search.status,
            page: search.page,
            pageSize: PAGE_SIZE,
          },
        },
      }),
    ),
    load((api) => api.GET("/api/academics/classrooms")),
  ]);
  const classList = classrooms.ok ? classrooms.data : [];
  const selectedClass = classList.find((c) => c.id === search.classRoomId);

  let body: React.ReactNode;
  if (!students.ok) {
    body = <ProblemState problem={students.problem} />;
  } else if (students.data.items.length === 0) {
    body =
      search.search || search.classRoomId || search.status !== "Active" ? (
        <EmptyState
          title="No students match"
          action={
            <Link href="/admin/students" className={buttonClasses("secondary")}>
              Clear filters
            </Link>
          }
        >
          Search matches every word against first, middle and last names, or the admission number.
        </EmptyState>
      ) : (
        <EmptyState icon={GraduationCap} title="No students yet" action={admit}>
          {isAdmin ? "Admit your first student. You can add guardians and issue logins from their profile." : "No students have been admitted yet."}
        </EmptyState>
      );
  } else {
    const rows = students.data.items;
    body = (
      <>
        <table className="hidden w-full text-left text-sm md:table">
          <caption className="sr-only">Students</caption>
          <thead className="bg-surface-2 text-xs uppercase tracking-wide text-muted">
            <tr>
              <th scope="col" className="px-4 py-2.5 font-medium">Name</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Admission no.</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Class</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Status</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Login</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {rows.map((s) => (
              <tr key={s.id} className="transition-colors hover:bg-surface-2/50">
                <th scope="row" className="px-4 py-3 font-normal">
                  <Link href={`/admin/students/${s.id}`} className="group flex items-center gap-3">
                    <Avatar name={`${s.firstName} ${s.lastName}`} className="size-8 text-xs" />
                    <span className="font-medium group-hover:text-brand group-hover:underline">{s.fullName}</span>
                  </Link>
                </th>
                <td className="tabular px-4 py-3 font-mono text-xs">{s.admissionNumber}</td>
                <td className="px-4 py-3">{s.classRoom ?? <span className="text-muted">No class</span>}</td>
                <td className="px-4 py-3">
                  <Badge tone={statusTone[s.status]}>{s.status}</Badge>
                </td>
                <td className="px-4 py-3">{s.hasLogin ? <Badge tone="success">Has login</Badge> : <Badge tone="warning">No login</Badge>}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <ul className="divide-y divide-border md:hidden">
          {rows.map((s) => (
            <li key={s.id}>
              <Link href={`/admin/students/${s.id}`} className="flex items-center gap-3 px-4 py-3">
                <Avatar name={`${s.firstName} ${s.lastName}`} className="size-9 text-xs" />
                <span className="min-w-0 flex-1">
                  <span className="block font-medium">{s.fullName}</span>
                  <span className="block truncate font-mono text-xs text-muted">
                    {s.admissionNumber} · {s.classRoom ?? "No class"}
                  </span>
                  <span className="mt-1 flex flex-wrap gap-1.5">
                    <Badge tone={statusTone[s.status]}>{s.status}</Badge>
                    {!s.hasLogin && <Badge tone="warning">No login</Badge>}
                  </span>
                </span>
                <ChevronRight className="size-4 text-muted" aria-hidden />
              </Link>
            </li>
          ))}
        </ul>
        <Pagination page={students.data.page} pageSize={students.data.pageSize} totalCount={students.data.totalCount} hrefFor={(page) => studentsHref(search, { page })} noun="students" />
      </>
    );
  }

  const batch = isAdmin && selectedClass && students.ok ? <BatchLoginsButton className={selectedClass.name} students={students.data.items} /> : null;

  return (
    <>
      <PageHeader
        title="Students"
        description={selectedClass ? `${selectedClass.name} · ${selectedClass.studentCount}/${selectedClass.capacity} students` : "Search and open any student's profile."}
        actions={
          batch || admit ? (
            <>
              {batch}
              {admit}
            </>
          ) : undefined
        }
      />
      <StudentFilters search={search} classrooms={classList}>
        {body}
      </StudentFilters>
    </>
  );
}
