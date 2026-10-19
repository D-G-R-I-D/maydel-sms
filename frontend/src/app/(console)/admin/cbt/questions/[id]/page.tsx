import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { bankHref, kindLabel } from "@/lib/cbt/questions";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { QuestionEditor } from "@/components/cbt/question-editor";

export const metadata: Metadata = { title: "Question" };

const timeFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });

/** E16.1: edit one question (or archive it). Others get 404, so they can't tell it exists. */
export default async function QuestionPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const { id } = await params;
  const fromNew = (await searchParams).from === "new";
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        <PageHeader title="Question" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  if (!GUID.test(id)) notFound();
  const question = await load((api) => api.GET("/api/cbt/questions/{id}", { params: { path: { id } } }));
  if (!question.ok) {
    if (question.problem.status === 404) notFound();
    return (
      <>
        <PageHeader title="Question" />
        <Card>
          <ProblemState problem={question.problem} />
        </Card>
      </>
    );
  }
  const q = question.data;
  const topics = await load((api) => api.GET("/api/cbt/topics", { params: { query: { subjectId: q.subjectId, classLevelId: q.classLevelId } } }));
  const scope = { subjectId: q.subjectId, classLevelId: q.classLevelId, subject: q.subject, classLevel: q.classLevel };

  return (
    <>
      {fromNew ? (
        <Link
          data-keeps-draft
          href={`/admin/cbt/questions/new?${new URLSearchParams({ subjectId: q.subjectId, classLevelId: q.classLevelId }).toString()}`}
          className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline"
        >
          <ArrowLeft className="size-4" aria-hidden />
          Back to the question you were writing
        </Link>
      ) : (
        <Link href={bankHref(q.subjectId, q.classLevelId)} className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
          <ArrowLeft className="size-4" aria-hidden />
          {q.subject} · {q.classLevel}
        </Link>
      )}
      <PageHeader
        title="Edit question"
        description={
          <>
            {q.createdBy ? `Added by ${q.createdBy}` : "Added"} on {timeFormat.format(new Date(q.createdAt))}
            {q.updatedAt ? ` · last changed ${timeFormat.format(new Date(q.updatedAt))}` : ""}
          </>
        }
        actions={
          <span className="flex flex-wrap gap-1.5">
            <Badge>{kindLabel[q.kind]}</Badge>
            <Badge tone={q.isAutoMarked ? "brand" : "neutral"}>{q.isAutoMarked ? "Auto-marked" : "Teacher-marked"}</Badge>
            {q.source === "Ai" && <Badge tone="warning">Written with AI</Badge>}
          </span>
        }
      />
      <QuestionEditor scope={scope} question={q} topics={topics.ok ? topics.data : []} />
    </>
  );
}
