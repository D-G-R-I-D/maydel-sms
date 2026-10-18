import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { bankHref } from "@/lib/cbt/questions";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { QuestionEditor } from "@/components/cbt/question-editor";

export const metadata: Metadata = { title: "Add question" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E16.1: add a question to one bank (subject · class level). */
export default async function NewQuestionPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const sp = await searchParams;
  const subjectId = one(sp.subjectId);
  const classLevelId = one(sp.classLevelId);
  if (!subjectId || !classLevelId || !GUID.test(subjectId) || !GUID.test(classLevelId)) notFound();
  const back = (
    <Link href={bankHref(subjectId, classLevelId)} className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Back to the bank
    </Link>
  );
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        {back}
        <PageHeader title="Add question" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const [scopes, topics] = await Promise.all([
    load((api) => api.GET("/api/cbt/scopes")),
    load((api) => api.GET("/api/cbt/topics", { params: { query: { subjectId, classLevelId } } })),
  ]);
  if (!scopes.ok) {
    return (
      <>
        {back}
        <PageHeader title="Add question" />
        <Card>
          <ProblemState problem={scopes.problem} />
        </Card>
      </>
    );
  }
  const scope = scopes.data.find((s) => s.subjectId === subjectId && s.classLevelId === classLevelId);
  if (!scope) {
    return (
      <>
        {back}
        <PageHeader title="Add question" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  return (
    <>
      {back}
      <PageHeader title="Add question" description={`${scope.subject} · ${scope.classLevel}`} />
      <QuestionEditor scope={scope} question={null} topics={topics.ok ? topics.data : []} />
    </>
  );
}
