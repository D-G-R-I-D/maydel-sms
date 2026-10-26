import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { addDays, todayWat } from "@/lib/clock/clock";
import { classChoices } from "@/lib/homework/homework";
import { GUID } from "@/lib/media/binary-proxy";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { HomeworkEditor } from "@/components/homework/homework-editor";

export const metadata: Metadata = { title: "Homework" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E17.1 staff: set new homework (`/new?class=&subject=`) or edit one. */
export default async function HomeworkPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
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
  const { id } = await params;
  const sp = await searchParams;
  const isNew = id === "new";
  if (!isNew && !GUID.test(id)) notFound();
  const [mine, hw] = await Promise.all([
    load((api) => api.GET("/api/assessment/my-classes")),
    isNew ? null : load((api) => api.GET("/api/homework/{id}", { params: { path: { id } } })),
  ]);
  if (hw && !hw.ok && hw.problem.status === 404) notFound();
  const choices = mine.ok ? classChoices(mine.data) : [];
  const today = todayWat();
  const back = (
    <Link href="/admin/homework" className="mb-3 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Homework
    </Link>
  );

  if (isNew && choices.length === 0) {
    return (
      <>
        {back}
        <PageHeader title="Set homework" />
        <Card>
          <EmptyState title="No classes to set homework for">You need to be assigned to teach a subject in a class first. Ask the school admin.</EmptyState>
        </Card>
      </>
    );
  }

  return (
    <>
      {back}
      <PageHeader title={isNew ? "Set homework" : hw?.ok ? hw.data.title : "Homework"} description={hw?.ok ? `${hw.data.classRoom} · ${hw.data.subject ?? "General"}` : undefined} />
      {hw && !hw.ok ? (
        <Card>
          <ProblemState problem={hw.problem} />
        </Card>
      ) : (
        <HomeworkEditor
          key={hw?.ok ? hw.data.id : "new"}
          initial={hw?.ok ? hw.data : null}
          choices={choices}
          isAdmin={hasAnyRole(user.roles, ["Admin"])}
          today={today}
          defaults={{ classRoomId: one(sp.class), subjectId: one(sp.subject), dueOn: addDays(today, 2) }}
        />
      )}
    </>
  );
}
