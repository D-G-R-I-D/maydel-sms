import type { Metadata } from "next";
import Link from "next/link";
import { ListChecks } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { isStudent } from "@/lib/auth/roles";
import { stateLabel, stateTone, windowLabel } from "@/lib/cbt/attempt";
import { formatMarks } from "@/lib/cbt/tests";
import { Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Tests" };

/** E16.3: the published tests set for the student's class: in progress, open, upcoming, then finished. */
export default async function PortalTestsPage() {
  const user = await requireSessionUser();
  const header = <PageHeader title="Tests" description="Computer-based tests your teachers have set. Your answers save as you go." />;
  if (!isStudent(user.roles)) {
    return (
      <>
        {header}
        <Card>
          <EmptyState icon={ListChecks} title="Tests are taken with the student's own account">
            Your child signs in with their admission number to sit a test. Their results appear under Results once the teacher releases them.
          </EmptyState>
        </Card>
      </>
    );
  }
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
