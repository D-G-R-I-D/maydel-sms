import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, CircleDot, MessageSquare } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { isStudent } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { attemptImageUrl } from "@/lib/cbt/attempt";
import { formatMarks } from "@/lib/cbt/tests";
import { formatLagos } from "@/lib/time/lagos";
import { Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Marked paper" };

/** E16.4: a student's own released paper: their answers, the mark for each question and comments. No answer keys. */
export default async function MarkedPaperPage({ params }: { params: Promise<{ attemptId: string }> }) {
  const { attemptId } = await params;
  const user = await requireSessionUser();
  const back = (
    <Link href="/portal/tests" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Tests
    </Link>
  );
  if (!isStudent(user.roles)) {
    return (
      <>
        {back}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  if (!GUID.test(attemptId)) notFound();
  const paper = await load((api) => api.GET("/api/student/attempts/{id}", { params: { path: { id: attemptId } } }));
  if (!paper.ok) {
    if (paper.problem.status === 404) notFound();
    return (
      <>
        {back}
        <Card>
          <ProblemState problem={paper.problem} />
        </Card>
      </>
    );
  }
  const p = paper.data;
  const items = [...p.items].sort((a, b) => a.section - b.section || a.order - b.order);
  const sectionTitle = new Map(p.sections.map((s) => [s.index, s.title]));

  return (
    <>
      {back}
      <PageHeader
        title={p.title}
        description={p.submittedAt ? `Handed in ${formatLagos(p.submittedAt)}${p.autoSubmitted ? " (when time ran out)" : ""}` : undefined}
        actions={
          p.released && p.score !== null ? (
            <p className="tabular font-display text-2xl font-semibold">
              {formatMarks(p.score)} <span className="text-base font-normal text-muted">/ {formatMarks(p.totalMarks)}</span>
            </p>
          ) : undefined
        }
      />
      {!p.released ? (
        <Card>
          <EmptyState title="Not released yet">Your teacher is still marking. Your score and their comments appear here once results are released.</EmptyState>
        </Card>
      ) : (
        <ol className="space-y-4">
          {items.map((item, i) => {
            const choice = item.kind === "Objective" || item.kind === "MultiSelect";
            const full = item.score !== null && item.score >= item.marks;
            return (
              <li key={item.id}>
                {(i === 0 || items[i - 1].section !== item.section) && <h2 className="mb-2 font-display text-lg font-semibold">{sectionTitle.get(item.section)}</h2>}
                <Card className="space-y-3 p-5">
                  <p className="flex items-baseline justify-between gap-3 text-sm">
                    <span className="text-muted">Question {i + 1}</span>
                    <span className={cn("tabular font-semibold", full ? "text-success" : item.score === 0 ? "text-danger" : "text-text")}>
                      {item.score === null ? "—" : formatMarks(item.score)} / {formatMarks(item.marks)}
                    </span>
                  </p>
                  <p className="whitespace-pre-wrap">{item.text}</p>
                  {/* The private BFF picture; next/image adds nothing here. */}
                  {item.imageUrl && <img src={attemptImageUrl(p.id, item.id)} alt={`Illustration for question ${i + 1}`} className="max-h-64 max-w-full rounded-input border border-border object-contain" />}
                  {choice ? (
                    <ul className="space-y-1.5" aria-label="Your answer">
                      {item.options.map((o, oi) => {
                        const chosen = item.answerOptionIds.includes(o.id);
                        return (
                          <li key={o.id} className={cn("flex items-center gap-2 rounded-input border px-3 py-2 text-sm", chosen ? "border-brand bg-brand/5" : "border-border")}>
                            <span className="font-medium text-muted">{String.fromCharCode(65 + oi)}.</span>
                            <span className="min-w-0 flex-1 break-words">{o.text}</span>
                            {chosen && (
                              <span className="inline-flex items-center gap-1 text-xs font-medium text-brand">
                                <CircleDot className="size-3.5" aria-hidden />
                                Your answer
                              </span>
                            )}
                          </li>
                        );
                      })}
                    </ul>
                  ) : (
                    <div className="rounded-input bg-surface-2 p-3 text-sm">
                      <p className="mb-1 text-xs font-medium text-muted">Your answer</p>
                      <p className="whitespace-pre-wrap break-words">
                        {item.kind === "TrueFalse" ? (item.answerTrueFalse === null ? "No answer" : item.answerTrueFalse ? "True" : "False") : item.answerText || <span className="text-muted">No answer</span>}
                      </p>
                    </div>
                  )}
                  {item.comment && (
                    <p className="flex gap-2 rounded-input border border-brand/30 bg-brand/5 p-3 text-sm">
                      <MessageSquare className="mt-0.5 size-4 shrink-0 text-brand" aria-hidden />
                      <span>
                        <span className="sr-only">Teacher&apos;s comment: </span>
                        {item.comment}
                      </span>
                    </p>
                  )}
                </Card>
              </li>
            );
          })}
        </ol>
      )}
    </>
  );
}
