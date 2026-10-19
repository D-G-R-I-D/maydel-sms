import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, ChevronLeft, ChevronRight } from "lucide-react";
import { load } from "@/lib/api/server";
import { GUID } from "@/lib/media/binary-proxy";
import { loadTestForPage } from "@/lib/cbt/test-page";
import { formatMarks } from "@/lib/cbt/tests";
import { formatLagos } from "@/lib/time/lagos";
import { Badge, Card, PageHeader, buttonClasses, cn } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { ReviewAnswer } from "@/components/cbt/review-answer";
import { MarkBox } from "@/components/cbt/mark-box";

export const metadata: Metadata = { title: "Marking" };

const eventLabel = { Blur: "Left the test", Focus: "Came back", TakeOver: "Opened elsewhere" } as const;

/** E16.4 by student: every answer next to the key or guide, with a mark box; Prev/Next student. */
export default async function MarkAttemptPage({ params }: { params: Promise<{ id: string; attemptId: string }> }) {
  const { id, attemptId } = await params;
  const test = await loadTestForPage(id);
  if (!test || !test.ok) {
    return (
      <>
        <PageHeader title="Marking" />
        <Card>{test ? <ProblemState problem={test.problem} /> : <AccessDenied />}</Card>
      </>
    );
  }
  if (!GUID.test(attemptId)) notFound();
  const [review, rows] = await Promise.all([
    load((api) => api.GET("/api/cbt/attempts/{id}", { params: { path: { id: attemptId } } })),
    load((api) => api.GET("/api/cbt/tests/{id}/attempts", { params: { path: { id } } })),
  ]);
  if (!review.ok) {
    if (review.problem.status === 404) notFound();
    return (
      <>
        <PageHeader title="Marking" />
        <Card>
          <ProblemState problem={review.problem} />
        </Card>
      </>
    );
  }
  const r = review.data;
  const order = rows.ok ? rows.data.students.filter((s) => s.state === "Submitted" && s.attemptId).map((s) => s.attemptId!) : [];
  const at = order.indexOf(r.id);
  const prev = at > 0 ? order[at - 1] : null;
  const next = at >= 0 && at < order.length - 1 ? order[at + 1] : null;
  const items = [...r.items].sort((a, b) => a.order - b.order);
  const step = (target: string | null, label: string, icon: React.ReactNode) =>
    target ? (
      <Link href={`/admin/tests/${id}/attempts/${target}`} className={buttonClasses("secondary", "sm")}>
        {icon}
        {label}
      </Link>
    ) : (
      <span className={cn(buttonClasses("secondary", "sm"), "pointer-events-none opacity-50")} aria-disabled="true">
        {icon}
        {label}
      </span>
    );

  return (
    <>
      <Link href={`/admin/tests/${id}/results`} className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        {test.data.title}: results
      </Link>
      <PageHeader
        title={r.studentName}
        description={
          <>
            <span className="font-mono">{r.admissionNumber}</span>
            {r.number > 1 ? ` · attempt ${r.number}` : ""} · handed in {r.submittedAt ? formatLagos(r.submittedAt) : "—"}
            {r.autoSubmitted ? " (when time ran out)" : ""}
          </>
        }
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <span className="tabular font-display text-lg font-semibold" data-testid="attempt-score">
              {r.score === null ? "—" : formatMarks(r.score)} / {formatMarks(r.totalMarks)}
            </span>
            {r.needsMarking > 0 ? <Badge tone="warning">{r.needsMarking} to mark</Badge> : <Badge tone="success">All marked</Badge>}
            {step(prev, "Previous student", <ChevronLeft className="size-4" aria-hidden />)}
            {step(next, "Next student", <ChevronRight className="size-4" aria-hidden />)}
          </div>
        }
      />
      {!r.canMark && <p className="mb-4 text-sm text-muted">Results are released, so marks can&apos;t change.</p>}

      <div className="grid gap-5 lg:grid-cols-[1fr_15rem] lg:items-start">
        <ol className="space-y-4">
          {items.map((item, i) => (
            <li key={item.id}>
              {(i === 0 || items[i - 1].section !== item.section) && <h2 className="mb-2 font-display text-lg font-semibold">{item.section}</h2>}
              <Card className={cn("grid gap-4 p-5 md:grid-cols-[1fr_14rem]", item.score === null && item.answered && "border-warning")}>
                <ReviewAnswer item={item} number={i + 1} />
                <MarkBox attemptId={r.id} itemId={item.id} marks={item.marks} score={item.score} autoScore={item.autoScore} comment={item.comment} canMark={r.canMark} label={`question ${i + 1}`} />
              </Card>
            </li>
          ))}
        </ol>
        <aside className="lg:sticky lg:top-4">
          <Card className="p-4">
            <h2 className="font-display font-semibold">Focus log</h2>
            {r.events.length === 0 ? (
              <p className="mt-1 text-sm text-muted">Stayed on the test throughout.</p>
            ) : (
              <ol className="mt-2 space-y-1 text-sm">
                {r.events.map((e, i) => (
                  <li key={i} className="flex justify-between gap-2">
                    <span>{eventLabel[e.kind]}</span>
                    <span className="tabular text-muted">{formatLagos(e.at).split(", ").pop()}</span>
                  </li>
                ))}
              </ol>
            )}
          </Card>
        </aside>
      </div>
    </>
  );
}
