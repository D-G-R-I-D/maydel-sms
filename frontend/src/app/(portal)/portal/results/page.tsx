import type { Metadata } from "next";
import Link from "next/link";
import { ChevronRight, ClipboardList, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { formatDay } from "@/lib/validation/academics";
import { formatAverage, ordinal } from "@/lib/reports/rules";
import { Card, PageHeader, cn } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { PhotoAvatar } from "@/components/media/photo-avatar";

export const metadata: Metadata = { title: "Results" };

/** Published results for one child (switcher for parents with several), grouped by session. */
export default async function PortalResultsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const sp = await searchParams;
  const requested = Array.isArray(sp.child) ? sp.child[0] : sp.child;
  const children = await load((api) => api.GET("/api/portal/children"));
  const header = <PageHeader title="Results" description="Published report cards. Drafts appear here once the school releases them." />;

  if (!children.ok) {
    return (
      <>
        {header}
        <Card>
          <ProblemState problem={children.problem} />
        </Card>
      </>
    );
  }
  if (children.data.length === 0) {
    return (
      <>
        {header}
        <Card>
          <EmptyState icon={Users} title="No children linked yet">
            Ask the school office to link your children to your account.
          </EmptyState>
        </Card>
      </>
    );
  }

  const child = children.data.find((c) => c.studentId === requested) ?? children.data[0];
  const results = await load((api) => api.GET("/api/portal/children/{studentId}/results", { params: { path: { studentId: child.studentId } } }));

  const sessions = results.ok ? [...new Set(results.data.map((r) => r.session))] : [];

  return (
    <>
      {header}
      {children.data.length > 1 && (
        <nav aria-label="Choose a child" className="-mt-2 mb-5 flex gap-2 overflow-x-auto pb-1">
          {children.data.map((c) => {
            const active = c.studentId === child.studentId;
            return (
              <Link
                key={c.studentId}
                href={`/portal/results?child=${c.studentId}`}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "inline-flex min-h-11 shrink-0 items-center gap-2 rounded-full border py-1 pr-4 pl-1 text-sm transition-colors",
                  active ? "border-brand bg-brand/10 font-medium text-brand" : "border-border bg-surface text-text hover:bg-surface-2",
                )}
              >
                <PhotoAvatar kind="portal" id={c.studentId} name={c.name} photoVersion={c.photoVersion} className="size-8 text-xs" />
                {c.name.split(" ")[0]}
              </Link>
            );
          })}
        </nav>
      )}

      <Card className="overflow-hidden">
        <div className="flex items-center gap-3 border-b border-border px-5 py-4">
          <PhotoAvatar kind="portal" id={child.studentId} name={child.name} photoVersion={child.photoVersion} className="size-11" />
          <div className="min-w-0">
            <h2 className="truncate font-display text-lg font-semibold">{child.name}</h2>
            <p className="text-sm text-muted">
              <span className="font-mono">{child.admissionNumber}</span>
              {child.classRoom ? ` · ${child.classRoom}` : ""}
            </p>
          </div>
        </div>

        {!results.ok ? (
          <ProblemState problem={results.problem} />
        ) : results.data.length === 0 ? (
          <EmptyState icon={ClipboardList} title="No results published yet">
            You&apos;ll see them here once the school releases them.
          </EmptyState>
        ) : (
          sessions.map((session) => (
            <section key={session} aria-labelledby={`s-${session}`}>
              <h3 id={`s-${session}`} className="bg-surface-2 px-5 py-2 text-xs font-semibold uppercase tracking-wider text-muted">
                {session}
              </h3>
              <ul className="divide-y divide-border">
                {results.data
                  .filter((r) => r.session === session)
                  .map((r) => (
                    <li key={r.termId}>
                      <Link href={`/portal/results/${child.studentId}/${r.termId}`} className="flex min-h-16 items-center gap-3 px-5 py-3 transition-colors hover:bg-surface-2/50">
                        <span className="min-w-0 flex-1">
                          <span className="block font-medium">{r.term}</span>
                          <span className="block text-sm text-muted">
                            {r.classRoom}
                            {r.publishedAt ? ` · released ${formatDay(r.publishedAt.slice(0, 10))}` : ""}
                          </span>
                        </span>
                        <span className="text-right">
                          <span className="tabular block font-display text-lg font-semibold">{formatAverage(r.average)}</span>
                          <span className="tabular block text-xs text-muted">{r.position ? `${ordinal(r.position)} of ${r.classSize}` : "average"}</span>
                        </span>
                        <span className="sr-only">View</span>
                        <ChevronRight className="size-4 text-muted" aria-hidden />
                      </Link>
                    </li>
                  ))}
              </ul>
            </section>
          ))
        )}
      </Card>
    </>
  );
}
