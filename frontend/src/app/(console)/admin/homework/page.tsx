import type { Metadata } from "next";
import Link from "next/link";
import { ClipboardList, Paperclip, Plus } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { todayWat } from "@/lib/clock/clock";
import { classChoices, dueLabel } from "@/lib/homework/homework";
import { Button, Card, PageHeader, Select, buttonClasses, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { HBar } from "@/components/cbt/bars";

export const metadata: Metadata = { title: "Homework" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;
const GUID = /^[0-9a-f-]{36}$/i;

/** E17.1 staff: homework for the classes you teach (admins: all), latest due first, with hand-in progress. */
export default async function HomeworkListPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        <PageHeader title="Homework" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const classRoomId = GUID.test(one(sp.class) ?? "") ? one(sp.class) : undefined;
  const subjectId = GUID.test(one(sp.subject) ?? "") ? one(sp.subject) : undefined;
  const page = Math.max(1, Number(one(sp.page)) || 1);
  const [mine, list] = await Promise.all([
    load((api) => api.GET("/api/assessment/my-classes")),
    load((api) => api.GET("/api/homework", { params: { query: { ...(classRoomId ? { classRoomId } : {}), ...(subjectId ? { subjectId } : {}), page, pageSize: 20 } } })),
  ]);
  const choices = mine.ok ? classChoices(mine.data) : [];
  const subjects = classRoomId ? (choices.find((c) => c.classRoomId === classRoomId)?.subjects ?? []) : [];
  const today = todayWat();
  const q = (p: number) => {
    const u = new URLSearchParams();
    if (classRoomId) u.set("class", classRoomId);
    if (subjectId) u.set("subject", subjectId);
    if (p > 1) u.set("page", String(p));
    const s = u.toString();
    return `/admin/homework${s ? `?${s}` : ""}`;
  };
  const add = (
    <Link href={`/admin/homework/new${classRoomId ? `?class=${classRoomId}${subjectId ? `&subject=${subjectId}` : ""}` : ""}`} className={buttonClasses("primary")}>
      <Plus className="size-4" aria-hidden />
      Set homework
    </Link>
  );

  return (
    <>
      <PageHeader title="Homework" description="Set homework for your classes, see who has handed in, and give feedback." actions={add} />
      <form action="/admin/homework" className="mb-5 flex flex-wrap items-end gap-3">
        <div className="space-y-1.5 text-sm">
          <label htmlFor="hw-f-class" className="block font-medium">
            Class
          </label>
          <Select id="hw-f-class" name="class" defaultValue={classRoomId ?? ""}>
            <option value="">All my classes</option>
            {choices.map((c) => (
              <option key={c.classRoomId} value={c.classRoomId}>
                {c.classRoom}
              </option>
            ))}
          </Select>
        </div>
        {subjects.length > 0 && (
          <div className="space-y-1.5 text-sm">
            <label htmlFor="hw-f-subject" className="block font-medium">
              Subject
            </label>
            <Select id="hw-f-subject" name="subject" defaultValue={subjectId ?? ""}>
              <option value="">All subjects</option>
              {subjects.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </Select>
          </div>
        )}
        <Button type="submit" variant="secondary">
          Show
        </Button>
      </form>
      {!list.ok ? (
        <Card>
          <ProblemState problem={list.problem} />
        </Card>
      ) : list.data.items.length === 0 ? (
        <Card>
          <EmptyState icon={ClipboardList} title="No homework yet" action={add}>
            Set the first piece for your class; students and parents are notified.
          </EmptyState>
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <ul className="divide-y divide-border">
            {list.data.items.map((h) => {
              const ratio = h.classSize ? (h.submitted ?? 0) / h.classSize : 0;
              const overdue = h.dueOn < today;
              return (
                <li key={h.id}>
                  <Link href={`/admin/homework/${h.id}`} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-5 py-3 hover:bg-surface-2">
                    <span className="min-w-0 flex-1">
                      <span className="flex items-center gap-2">
                        <span className="truncate font-medium">{h.title}</span>
                        {h.files.length > 0 && <Paperclip className="size-3.5 shrink-0 text-muted" aria-label={`${h.files.length} attachments`} />}
                      </span>
                      <span className="text-xs text-muted">
                        {h.classRoom} · {h.subject ?? "General"} · {h.setBy}
                      </span>
                    </span>
                    <span className={cn("text-sm", overdue ? "text-muted" : "font-medium")}>{dueLabel(h.dueOn, today)}</span>
                    {h.acceptsSubmissions && h.classSize != null && (
                      <span className="w-36">
                        <span className="tabular block text-right text-xs text-muted">
                          {h.submitted ?? 0}/{h.classSize} handed in
                        </span>
                        <HBar ratio={ratio} tone={ratio >= 1 ? "success" : "brand"} className="mt-1" />
                      </span>
                    )}
                  </Link>
                </li>
              );
            })}
          </ul>
          <div className="border-t border-border p-4">
            <Pagination page={list.data.page} pageSize={list.data.pageSize} totalCount={list.data.totalCount} hrefFor={q} noun="homework" />
          </div>
        </Card>
      )}
    </>
  );
}
