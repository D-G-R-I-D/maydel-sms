import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, FileQuestion, Plus } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { DIFFICULTIES, KINDS, difficultyTone, firstLine, kindLabel, type Difficulty, type QuestionKind } from "@/lib/cbt/questions";
import { Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { QuestionFilters } from "@/components/cbt/question-filters";

export const metadata: Metadata = { title: "Question bank" };

const PAGE_SIZE = 25;
const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E16.1: one bank (subject · class level), newest first, with kind/topic/difficulty/search filters. */
export default async function BankPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const back = (
    <Link href="/admin/cbt" className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
      <ArrowLeft className="size-4" aria-hidden />
      Question bank
    </Link>
  );
  const denied = (
    <>
      {back}
      <PageHeader title="Question bank" />
      <Card>
        <AccessDenied />
      </Card>
    </>
  );
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) return denied;
  const sp = await searchParams;
  const subjectId = one(sp.subjectId);
  const classLevelId = one(sp.classLevelId);
  if (!subjectId || !classLevelId || !GUID.test(subjectId) || !GUID.test(classLevelId)) notFound();
  const kindParam = one(sp.kind);
  const kind = kindParam && (KINDS as string[]).includes(kindParam) ? (kindParam as QuestionKind) : undefined;
  const difficultyParam = one(sp.difficulty);
  const difficulty = difficultyParam && (DIFFICULTIES as string[]).includes(difficultyParam) ? (difficultyParam as Difficulty) : undefined;
  const topic = one(sp.topic)?.slice(0, 80);
  const search = one(sp.search)?.slice(0, 100);
  const page = Math.max(1, Number(one(sp.page)) || 1);

  const [scopes, topics, questions] = await Promise.all([
    load((api) => api.GET("/api/cbt/scopes")),
    load((api) => api.GET("/api/cbt/topics", { params: { query: { subjectId, classLevelId } } })),
    load((api) => api.GET("/api/cbt/questions", { params: { query: { subjectId, classLevelId, kind, topic, difficulty, search, page, pageSize: PAGE_SIZE } } })),
  ]);
  if (!questions.ok && questions.problem.status === 403) return denied;
  const scope = scopes.ok ? scopes.data.find((s) => s.subjectId === subjectId && s.classLevelId === classLevelId) : undefined;
  const title = scope ? `${scope.subject} · ${scope.classLevel}` : "Question bank";
  const filtered = Boolean(kind || topic || difficulty || search);
  const scopeQuery = new URLSearchParams({ subjectId, classLevelId }).toString();
  const href = (p: number) => {
    const q = new URLSearchParams({ subjectId, classLevelId });
    if (kind) q.set("kind", kind);
    if (topic) q.set("topic", topic);
    if (difficulty) q.set("difficulty", difficulty);
    if (search) q.set("search", search);
    if (p > 1) q.set("page", String(p));
    return `/admin/cbt/bank?${q.toString()}`;
  };
  const addButton = (
    <Link href={`/admin/cbt/questions/new?${scopeQuery}`} className={buttonClasses("primary")}>
      <Plus className="size-4" aria-hidden />
      Add question
    </Link>
  );

  return (
    <>
      {back}
      <PageHeader title={title} description="Shared by everyone who teaches this subject at this level. Students never see the answers." actions={addButton} />
      <QuestionFilters topics={topics.ok ? topics.data : []} />
      <Card className="overflow-hidden">
        {!questions.ok ? (
          <ProblemState problem={questions.problem} />
        ) : questions.data.items.length === 0 ? (
          <EmptyState icon={FileQuestion} title={filtered ? "No questions match" : "No questions yet"}>
            {filtered ? "Try another search or filter." : "Add the first one."}
          </EmptyState>
        ) : (
          <>
            <ul className="divide-y divide-border">
              {questions.data.items.map((q) => (
                <li key={q.id}>
                  <Link href={`/admin/cbt/questions/${q.id}`} className="group flex flex-col gap-2 px-4 py-3 hover:bg-surface-2 sm:flex-row sm:items-center sm:gap-4">
                    <span className="min-w-0 flex-1">
                      <span className="block font-medium break-words group-hover:text-brand">{firstLine(q.text)}</span>
                      <span className="block text-xs text-muted">
                        {q.topic ?? "No topic"}
                        {q.createdBy ? ` · ${q.createdBy}` : ""}
                      </span>
                    </span>
                    <span className="flex flex-wrap gap-1.5">
                      <Badge>{kindLabel[q.kind]}</Badge>
                      <Badge tone={difficultyTone[q.difficulty]}>{q.difficulty}</Badge>
                      <Badge tone={q.isAutoMarked ? "brand" : "neutral"}>{q.isAutoMarked ? "Auto-marked" : "Teacher-marked"}</Badge>
                      {q.source === "Ai" && <Badge tone="warning">AI</Badge>}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={questions.data.totalCount} hrefFor={href} noun="questions" />
          </>
        )}
      </Card>
    </>
  );
}
