import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight, FileText, Star } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TermPicker } from "@/components/assessment/term-picker";

export const metadata: Metadata = { title: "Reports" };

/** Classes whose reports this user can open: admins see every class; teachers their form and taught classes. */
export default async function ReportsIndexPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        <PageHeader title="Reports" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const sp = await searchParams;
  const [classes, { terms, selected }] = await Promise.all([
    load((api) => api.GET("/api/assessment/my-classes")),
    loadTerms(Array.isArray(sp.termId) ? sp.termId[0] : sp.termId),
  ]);

  const header = (
    <PageHeader
      title="Reports"
      description={isAdmin ? "Every class's term reports: check, comment, publish." : "Report cards for your classes. Form teachers add comments and attendance."}
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

  const unique = new Map<string, { name: string; order: number; isForm: boolean }>();
  for (const c of classes.data) {
    const g = unique.get(c.classRoomId) ?? { name: c.classRoom, order: c.levelOrder, isForm: false };
    g.isForm ||= c.isFormClass;
    unique.set(c.classRoomId, g);
  }
  const list = [...unique.entries()].sort(([, a], [, b]) => Number(b.isForm) - Number(a.isForm) || a.order - b.order || a.name.localeCompare(b.name));

  if (!selected || list.length === 0) {
    return (
      <>
        {header}
        <Card>
          <EmptyState icon={FileText} title={!selected ? "No terms yet" : "No classes yet"}>
            {!selected ? "Reports are per term. Set up the session and terms first." : "You're not assigned to any class yet."}
          </EmptyState>
        </Card>
      </>
    );
  }

  return (
    <>
      {header}
      <p className="-mt-2 mb-5 text-sm text-muted">
        {selected.label} {selected.session}
        {selected.isCurrent ? " (current term)" : ""}
      </p>
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {list.map(([id, c], i) => (
          <li key={id} className={cn("animate-rise", ["", "delay-1", "delay-2", "delay-3"][Math.min(i, 3)])}>
            <Link href={`/admin/reports/${id}?termId=${selected.id}`} className="group block h-full">
              <Card className="card-interactive flex h-full items-center gap-3 p-4">
                <span className="inline-flex size-10 items-center justify-center rounded-full bg-brand/10 text-brand">
                  <FileText className="size-5" aria-hidden />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block font-display text-lg font-semibold">{c.name}</span>
                  {c.isForm && (
                    <Badge tone="brand" className="mt-0.5">
                      <Star className="size-3" aria-hidden />
                      Form class
                    </Badge>
                  )}
                </span>
                <ArrowRight className="size-4 text-muted transition-transform group-hover:translate-x-0.5 group-hover:text-brand" aria-hidden />
              </Card>
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}
