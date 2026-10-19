import type { Metadata } from "next";
import Link from "next/link";
import { ListChecks, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { isStudent } from "@/lib/auth/roles";
import { stateLabel, stateTone, windowLabel } from "@/lib/cbt/attempt";
import { formatMarks } from "@/lib/cbt/tests";
import { Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { ChildSwitcher } from "@/components/portal/child-switcher";
import { formatLagos } from "@/lib/time/lagos";

export const metadata: Metadata = { title: "Tests" };

/**
 * E16.3/E16.4: students see the tests set for their class (in progress, open, upcoming, then finished)
 * with released scores; parents see each child's released test results.
 */
export default async function PortalTestsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!isStudent(user.roles)) {
    const sp = await searchParams;
    return <ParentTests child={Array.isArray(sp.child) ? sp.child[0] : sp.child} />;
  }
  const header = <PageHeader title="Tests" description="Computer-based tests your teachers have set. Your answers save as you go." />;
  const tests = await load((api) => api.GET("/api/student/tests"));
  // Rendered per request, so the window labels are as of now.
  const now = new Date().getTime();

  return (
    <>
      {header}
      {!tests.ok ? (
        <Card>
          <ProblemState problem={tests.problem} />
        </Card>
      ) : tests.data.length === 0 ? (
        <Card>
          <EmptyState icon={ListChecks} title="No tests yet">
            When a teacher sets a test for your class, it appears here.
          </EmptyState>
        </Card>
      ) : (
        <ul className="grid gap-3 sm:grid-cols-2">
          {tests.data.map((t) => {
            const left = t.maxAttempts - t.attemptsUsed;
            return (
              <li key={t.id}>
                <Card className="flex h-full flex-col gap-3 p-5">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="text-sm text-muted">{t.subject}</p>
                      <h2 className="font-display text-lg font-semibold break-words">{t.title}</h2>
                    </div>
                    <Badge tone={stateTone[t.state]}>{stateLabel[t.state]}</Badge>
                  </div>
                  <p className="text-sm">{windowLabel(t, now)}</p>
                  <p className="tabular text-sm text-muted">
                    {t.durationMinutes} min · {t.questionCount} {t.questionCount === 1 ? "question" : "questions"} · {formatMarks(t.totalMarks)} marks
                    {t.maxAttempts > 1 ? ` · ${t.attemptsUsed} of ${t.maxAttempts} attempts used` : ""}
                  </p>
                  <div className="mt-auto pt-1">
                    {t.state === "InProgress" ? (
                      <Link href={`/exam/${t.id}`} className={buttonClasses("primary")}>
                        Continue
                      </Link>
                    ) : t.state === "Open" && left > 0 ? (
                      <Link href={`/exam/${t.id}`} className={buttonClasses("primary")}>
                        {t.attemptsUsed > 0 ? "Try again" : "Start"}
                      </Link>
                    ) : t.resultsReleased && t.score !== null ? (
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <p className="tabular font-display text-xl font-semibold">
                          {formatMarks(t.score)} <span className="text-base font-normal text-muted">/ {formatMarks(t.totalMarks)}</span>
                        </p>
                        {t.resultAttemptId && (
                          <Link href={`/portal/tests/attempts/${t.resultAttemptId}`} className="text-sm text-brand hover:underline">
                            View marked paper
                          </Link>
                        )}
                      </div>
                    ) : t.state === "Done" ? (
                      <p className="text-sm text-muted">Handed in. Your teacher will release the results.</p>
                    ) : null}
                  </div>
                </Card>
              </li>
            );
          })}
        </ul>
      )}
    </>
  );
}

/** A parent's view: each child's released results (children sit tests with their own account). */
async function ParentTests({ child: requested }: { child?: string }) {
  const header = <PageHeader title="Tests" description="Computer-based test results your children's teachers have released." />;
  const children = await load((api) => api.GET("/api/portal/children"));
  if (!children.ok || children.data.length === 0) {
    return (
      <>
        {header}
        <Card>
          {!children.ok ? (
            <ProblemState problem={children.problem} />
          ) : (
            <EmptyState icon={Users} title="No children linked yet">
              Ask the school office to link your children to your account.
            </EmptyState>
          )}
        </Card>
      </>
    );
  }
  const child = children.data.find((c) => c.studentId === requested) ?? children.data[0];
  const results = await load((api) => api.GET("/api/portal/children/{studentId}/tests", { params: { path: { studentId: child.studentId } } }));
  return (
    <>
      {header}
      <ChildSwitcher items={children.data} activeId={child.studentId} basePath="/portal/tests" />
      {!results.ok ? (
        <Card>
          <ProblemState problem={results.problem} />
        </Card>
      ) : results.data.length === 0 ? (
        <Card>
          <EmptyState icon={ListChecks} title={`No released results for ${child.name.split(" ")[0]} yet`}>
            {child.name.split(" ")[0]} sits tests with their own account. Scores appear here once the teacher releases them.
          </EmptyState>
        </Card>
      ) : (
        <ul className="grid gap-3 sm:grid-cols-2" aria-label={`${child.name}'s test results`}>
          {results.data.map((r) => (
            <li key={r.testId}>
              <Card className="flex h-full items-center gap-4 p-5">
                <div className="min-w-0 flex-1">
                  <p className="text-sm text-muted">{r.subject}</p>
                  <h2 className="font-display text-lg font-semibold break-words">{r.title}</h2>
                  {r.submittedAt && <p className="text-xs text-muted">Handed in {formatLagos(r.submittedAt)}</p>}
                </div>
                <div className="text-right">
                  <p className="tabular font-display text-xl font-semibold">
                    {formatMarks(r.score)}
                    <span className="text-base font-normal text-muted"> / {formatMarks(r.totalMarks)}</span>
                  </p>
                  <p className="tabular text-sm text-muted">{r.percent}%</p>
                </div>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
