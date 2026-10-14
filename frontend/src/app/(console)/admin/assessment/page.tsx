import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight, BookOpen, ClipboardList, FileText, Star } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TermPicker } from "@/components/assessment/term-picker";

export const metadata: Metadata = { title: "Score entry" };

export default async function MyClassesPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        <PageHeader title="Score entry" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  const params = await searchParams;
  const requested = Array.isArray(params.termId) ? params.termId[0] : params.termId;
  const [classes, { terms, selected }] = await Promise.all([load((api) => api.GET("/api/assessment/my-classes")), loadTerms(requested)]);

  const header = (
    <PageHeader
      title="Score entry"
      description={hasAnyRole(user.roles, ["Admin"]) ? "Every class and subject. Pick one to enter or check scores." : "The classes and subjects you can grade."}
      actions={selected ? <TermPicker terms={terms} value={selected.id} /> : undefined}
    />
  );

  if (!classes.ok) {
    return (
      <>
        {header}
        <Card>
          <ProblemState problem={classes.problem} />
        </Card>
      </>
    );
  }
  if (!selected) {
    return (
      <>
        {header}
        <Card>
          <EmptyState icon={ClipboardList} title="No terms yet">
            Scores are entered per term. An administrator needs to set up the session and terms first (Academics → Sessions &amp; terms).
          </EmptyState>
        </Card>
      </>
    );
  }
  if (classes.data.length === 0) {
    return (
      <>
        {header}
        <Card>
          <EmptyState icon={ClipboardList} title="No classes to grade">
            You&apos;re not assigned to any class or subject yet. Ask an administrator to assign you under Academics → Classes.
          </EmptyState>
        </Card>
      </>
    );
  }

  // Group by class, in school order; form classes first.
  const byClass = new Map<string, { name: string; order: number; isForm: boolean; subjects: { id: string; name: string }[] }>();
  for (const c of classes.data) {
    const g = byClass.get(c.classRoomId) ?? { name: c.classRoom, order: c.levelOrder, isForm: false, subjects: [] };
    g.isForm ||= c.isFormClass;
    g.subjects.push({ id: c.subjectId, name: c.subject });
    byClass.set(c.classRoomId, g);
  }
  const groups = [...byClass.entries()].sort(([, a], [, b]) => Number(b.isForm) - Number(a.isForm) || a.order - b.order || a.name.localeCompare(b.name));

  return (
    <>
      {header}
      <p className="-mt-2 mb-5 text-sm text-muted">
        Showing <span className="font-medium text-text">{selected.label} {selected.session}</span>
        {selected.isCurrent ? " (current term)" : ""}.
      </p>
      <div className="grid gap-4 lg:grid-cols-2">
        {groups.map(([classRoomId, g], i) => (
          <Card key={classRoomId} className={cn("animate-rise overflow-hidden", ["", "delay-1", "delay-2", "delay-3"][Math.min(i, 3)])}>
            <div className="flex items-center gap-2 border-b border-border px-5 py-3">
              <h2 className="flex-1 font-display text-lg font-semibold">{g.name}</h2>
              {g.isForm && (
                <Badge tone="brand">
                  <Star className="size-3" aria-hidden />
                  Form class
                </Badge>
              )}
              <span className="text-xs text-muted">
                {g.subjects.length} {g.subjects.length === 1 ? "subject" : "subjects"}
              </span>
              <Link href={`/admin/reports/${classRoomId}?termId=${selected.id}`} className="inline-flex min-h-9 items-center gap-1 rounded-input px-2 text-xs font-medium text-brand hover:bg-brand/10">
                <FileText className="size-3.5" aria-hidden />
                Reports
              </Link>
            </div>
            <ul className="divide-y divide-border">
              {g.subjects
                .sort((a, b) => a.name.localeCompare(b.name))
                .map((s) => (
                  <li key={s.id}>
                    <Link
                      href={`/admin/assessment/${classRoomId}/${s.id}?termId=${selected.id}`}
                      className="group flex min-h-12 items-center gap-3 px-5 py-2.5 transition-colors hover:bg-surface-2/60"
                    >
                      <BookOpen className="size-4 text-muted" aria-hidden />
                      <span className="flex-1 text-sm font-medium group-hover:text-brand">{s.name}</span>
                      <span className="flex items-center gap-1 text-xs text-brand opacity-0 transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100">
                        Enter scores
                        <ArrowRight className="size-3.5" aria-hidden />
                      </span>
                    </Link>
                  </li>
                ))}
            </ul>
          </Card>
        ))}
      </div>
    </>
  );
}
