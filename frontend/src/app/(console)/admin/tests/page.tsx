import type { Metadata } from "next";
import Link from "next/link";
import { ListChecks, Plus } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { formatMarks, type TestStatus } from "@/lib/cbt/tests";
import { formatLagos } from "@/lib/time/lagos";
import { Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TermPicker } from "@/components/assessment/term-picker";
import { TestFilters } from "@/components/cbt/test-filters";

export const metadata: Metadata = { title: "Tests & exams" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E16.2: the term's tests the caller can manage, by opening time, with subject/level/status filters. */
export default async function TestsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        <PageHeader title="Tests & exams" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const { terms, selected } = await loadTerms(one(sp.termId));
  if (!selected) {
    return (
      <>
        <PageHeader title="Tests & exams" />
        <Card>
          <EmptyState title="No terms yet">Set up the school calendar under Academics first.</EmptyState>
        </Card>
      </>
    );
  }
  const subjectId = one(sp.subjectId);
  const classLevelId = one(sp.classLevelId);
  const statusParam = one(sp.status);
  const status = statusParam === "Draft" || statusParam === "Published" ? (statusParam as TestStatus) : undefined;
  const [list, scopes] = await Promise.all([
    load((api) => api.GET("/api/cbt/tests", { params: { query: { termId: selected.id, subjectId, classLevelId, status } } })),
    load((api) => api.GET("/api/cbt/scopes")),
  ]);
  const subjects = new Map<string, string>();
  const levels = new Map<string, string>();
  for (const s of scopes.ok ? scopes.data : []) {
    subjects.set(s.subjectId, s.subject);
    levels.set(s.classLevelId, s.classLevel);
  }
  const filtered = Boolean(subjectId || classLevelId || status);
  const canCreate = scopes.ok && scopes.data.length > 0;

  return (
    <>
      <PageHeader
        title="Tests & exams"
        description="Computer-based tests built from the question bank. Classes see a test only once it's published, inside its window."
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <TermPicker terms={terms} value={selected.id} />
            {canCreate && (
              <Link href={`/admin/tests/new?termId=${selected.id}`} className={buttonClasses("primary")}>
                <Plus className="size-4" aria-hidden />
                New test
              </Link>
            )}
          </div>
        }
      />
      <TestFilters subjects={[...subjects].map(([id, name]) => ({ id, name }))} levels={[...levels].map(([id, name]) => ({ id, name }))} />
      <Card className="overflow-hidden">
        {!list.ok ? (
          <ProblemState problem={list.problem} />
        ) : list.data.length === 0 ? (
          <EmptyState icon={ListChecks} title={filtered ? "No tests match" : "No tests this term"}>
            {filtered ? "Try another filter." : canCreate ? "Create one from your question banks." : "You can set tests for the subjects you teach. Ask an administrator to assign you."}
          </EmptyState>
        ) : (
          <ul className="divide-y divide-border">
            {list.data.map((t) => (
              <li key={t.id}>
                <Link href={`/admin/tests/${t.id}`} className="group flex flex-col gap-2 px-4 py-3 hover:bg-surface-2 md:flex-row md:items-center md:gap-4">
                  <span className="min-w-0 flex-1">
                    <span className="block font-medium break-words group-hover:text-brand">{t.title}</span>
                    <span className="block text-sm text-muted">
                      {t.subject} · {t.classLevel}
                      {t.classRooms.length > 0 ? ` · ${t.classRooms.join(", ")}` : ""}
                    </span>
                    <span className="block text-xs text-muted">
                      {formatLagos(t.opensAt)} to {formatLagos(t.closesAt)} · {t.durationMinutes} min
                    </span>
                  </span>
                  <span className="tabular text-sm text-muted md:w-36 md:text-right">
                    {t.questionCount} {t.questionCount === 1 ? "question" : "questions"} · {formatMarks(t.totalMarks)} marks
                  </span>
                  <span className="flex flex-wrap gap-1.5 md:w-44 md:justify-end">
                    <Badge tone={t.component ? "brand" : "neutral"}>{t.component ?? "Practice"}</Badge>
                    <Badge tone={t.status === "Published" ? "success" : "warning"}>{t.status}</Badge>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </>
  );
}
