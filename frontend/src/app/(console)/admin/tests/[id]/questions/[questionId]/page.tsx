import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Inbox } from "lucide-react";
import { load } from "@/lib/api/server";
import { GUID } from "@/lib/media/binary-proxy";
import { loadTestForPage } from "@/lib/cbt/test-page";
import { kindLabel } from "@/lib/cbt/questions";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { MarkBox } from "@/components/cbt/mark-box";

export const metadata: Metadata = { title: "Mark by question" };

/** E16.4 by question: every student's answer to one question, unmarked first, for consistent marking. */
export default async function MarkQuestionPage({ params }: { params: Promise<{ id: string; questionId: string }> }) {
  const { id, questionId } = await params;
  const test = await loadTestForPage(id);
  if (!test || !test.ok) {
    return (
      <>
        <PageHeader title="Mark by question" />
        <Card>{test ? <ProblemState problem={test.problem} /> : <AccessDenied />}</Card>
      </>
    );
  }
  if (!GUID.test(questionId)) notFound();
  const res = await load((api) => api.GET("/api/cbt/tests/{id}/questions/{questionId}/responses", { params: { path: { id, questionId } } }));
  if (!res.ok && res.problem.status === 404) notFound();
  const canMark = !test.data.resultsReleasedAt;

  return (
    <>
      <Link href={`/admin/tests/${id}/results`} className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        {test.data.title}: results
      </Link>
      <PageHeader title="Mark by question" description="Marks save when you leave each box. Unmarked answers are listed first." />
      {!res.ok ? (
        <Card>
          <ProblemState problem={res.problem} />
        </Card>
      ) : (
        <div className="space-y-4">
          <Card className="space-y-3 p-5">
            <Badge>{kindLabel[res.data.kind]}</Badge>
            <p className="whitespace-pre-wrap">{res.data.text}</p>
            {res.data.acceptedAnswers.length > 0 && (
              <p className="text-sm">
                <span className="text-muted">Accepted: </span>
                {res.data.acceptedAnswers.join(" · ")}
              </p>
            )}
            {res.data.markingGuide && (
              <div className="rounded-input border border-brand/30 bg-brand/5 p-3 text-sm">
                <p className="mb-1 text-xs font-medium text-brand">Marking guide</p>
                <p className="whitespace-pre-wrap">{res.data.markingGuide}</p>
              </div>
            )}
          </Card>
          {res.data.responses.length === 0 ? (
            <Card>
              <EmptyState icon={Inbox} title="No answers yet">
                Answers appear here once students hand in.
              </EmptyState>
            </Card>
          ) : (
            <ul className="space-y-3" aria-label="Answers">
              {res.data.responses.map((r) => (
                <li key={r.itemId}>
                  <Card className={cn("grid gap-4 p-4 md:grid-cols-[1fr_14rem]", r.score === null && r.answered && "border-warning")}>
                    <div className="min-w-0">
                      <p className="mb-1 flex flex-wrap items-center gap-2 text-sm font-medium">
                        {r.studentName}
                        {r.score === null && r.answered && <Badge tone="warning">To mark</Badge>}
                      </p>
                      <p className="text-sm whitespace-pre-wrap break-words">{r.answered && r.answerText ? r.answerText : <span className="text-muted">No answer</span>}</p>
                    </div>
                    <MarkBox attemptId={r.attemptId} itemId={r.itemId} marks={r.marks} score={r.score} autoScore={r.autoScore} comment={r.comment} canMark={canMark} label={r.studentName} />
                  </Card>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </>
  );
}
