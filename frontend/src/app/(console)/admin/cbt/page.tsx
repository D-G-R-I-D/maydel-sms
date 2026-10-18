import type { Metadata } from "next";
import Link from "next/link";
import { ChevronRight, FileQuestion } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { bankHref, type AuthoringScope } from "@/lib/cbt/questions";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Question bank" };

/** E16.1: the banks the caller can write for, one per subject and class level, grouped by level. */
export default async function QuestionBankPage() {
  const user = await requireSessionUser();
  const header = <PageHeader title="Question bank" description="Questions for computer-based tests, shared by everyone who teaches the subject at that level." />;
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        {header}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const scopes = await load((api) => api.GET("/api/cbt/scopes"));
  if (!scopes.ok) {
    return (
      <>
        {header}
        <Card>
          <ProblemState problem={scopes.problem} />
        </Card>
      </>
    );
  }
  const levels = new Map<string, { name: string; items: AuthoringScope[] }>();
  for (const s of scopes.data) {
    const level = levels.get(s.classLevelId) ?? { name: s.classLevel, items: [] };
    level.items.push(s);
    levels.set(s.classLevelId, level);
  }

  return (
    <>
      {header}
      {scopes.data.length === 0 ? (
        <Card>
          <EmptyState icon={FileQuestion} title="No question banks for you yet">
            You can write questions for the subjects you teach, and for any subject at a level where you&apos;re a form teacher. Ask an administrator to assign you.
          </EmptyState>
        </Card>
      ) : (
        <div className="space-y-6">
          {[...levels.entries()].map(([id, level]) => (
            <section key={id} aria-labelledby={`level-${id}`}>
              <h2 id={`level-${id}`} className="mb-2 font-display text-lg font-semibold">
                {level.name}
              </h2>
              <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {level.items.map((s) => (
                  <li key={s.subjectId}>
                    <Link href={bankHref(s.subjectId, s.classLevelId)} className="group block h-full">
                      <Card className="flex h-full items-center gap-3 p-4 transition-colors group-hover:border-brand">
                        <span className="min-w-0 flex-1">
                          <span className="block truncate font-medium group-hover:text-brand">{s.subject}</span>
                          <span className="tabular block text-sm text-muted">
                            {s.questions === 0 ? "No questions yet" : `${s.questions} ${s.questions === 1 ? "question" : "questions"}`}
                          </span>
                        </span>
                        <ChevronRight className="size-4 shrink-0 text-muted" aria-hidden />
                      </Card>
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      )}
    </>
  );
}
