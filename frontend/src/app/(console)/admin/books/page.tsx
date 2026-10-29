import type { Metadata } from "next";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { classChoices } from "@/lib/homework/homework";
import { BookList } from "@/components/book/book-list";
import { Button, Card, PageHeader, Select } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Communication books" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E17.2 staff: the form class's books (form teacher), or for admins every book with unread notes. */
export default async function BooksPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        <PageHeader title="Communication books" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const mine = await load((api) => api.GET("/api/assessment/my-classes"));
  const formClasses = (mine.ok ? classChoices(mine.data) : []).filter((c) => isAdmin || c.isFormClass);
  const requested = one((await searchParams).class);
  const classRoomId = formClasses.some((c) => c.classRoomId === requested) ? requested : isAdmin ? undefined : formClasses[0]?.classRoomId;
  const header = <PageHeader title="Communication books" description="A running notebook between the school and each family. Parents and students see the same notes." />;

  if (!isAdmin && formClasses.length === 0) {
    return (
      <>
        {header}
        <Card>
          <EmptyState title="Only form teachers keep communication books">If you are a form teacher, ask the admin to set you as the form teacher of your class.</EmptyState>
        </Card>
      </>
    );
  }
  const books = await load((api) => api.GET("/api/books", { params: { query: classRoomId ? { classRoomId } : {} } }));

  return (
    <>
      {header}
      {(formClasses.length > 1 || isAdmin) && (
        <form action="/admin/books" className="mb-5 flex flex-wrap items-end gap-3">
          <div className="space-y-1.5 text-sm">
            <label htmlFor="bk-class" className="block font-medium">
              Class
            </label>
            <Select id="bk-class" name="class" defaultValue={classRoomId ?? ""}>
              {isAdmin && <option value="">Unread notes, all classes</option>}
              {formClasses.map((c) => (
                <option key={c.classRoomId} value={c.classRoomId}>
                  {c.classRoom}
                </option>
              ))}
            </Select>
          </div>
          <Button type="submit" variant="secondary">
            Show
          </Button>
        </form>
      )}
      {!books.ok ? (
        <Card>
          <ProblemState problem={books.problem} />
        </Card>
      ) : (
        <BookList books={books.data} hrefFor={(b) => `/admin/books/${b.studentId}`} empty={classRoomId ? "No students in this class" : "No unread notes. You're up to date."} />
      )}
    </>
  );
}
