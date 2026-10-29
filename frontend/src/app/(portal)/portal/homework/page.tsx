import type { Metadata } from "next";
import Link from "next/link";
import { ClipboardList, Paperclip, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { todayWat } from "@/lib/clock/clock";
import { dueLabel, stateLabel, stateTone } from "@/lib/homework/homework";
import { ChildSwitcher } from "@/components/portal/child-switcher";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Homework" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E17.1 portal: a child's homework, soonest due first, with hand-in state and feedback. */
export default async function PortalHomeworkPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const sp = await searchParams;
  const requested = one(sp.child);
  const showAll = one(sp.view) === "all";
  const children = await load((api) => api.GET("/api/portal/children"));
  const header = <PageHeader title="Homework" description="What's been set, when it's due, and what the teacher said." />;

  if (!children.ok || children.data.length === 0) {
    return (
      <>
        {header}
        <Card>
          {children.ok ? (
            <EmptyState icon={Users} title="No children linked yet">
              Ask the school office to link your children to your account.
            </EmptyState>
          ) : (
            <ProblemState problem={children.problem} />
          )}
        </Card>
      </>
    );
  }
  const child = children.data.find((c) => c.studentId === requested) ?? children.data[0];
  const list = await load((api) => api.GET("/api/portal/children/{studentId}/homework", { params: { path: { studentId: child.studentId }, query: showAll ? {} : { open: true } } }));
  const today = todayWat();
  const base = `/portal/homework?child=${child.studentId}`;

  return (
    <>
      {header}
      <ChildSwitcher items={children.data} activeId={child.studentId} basePath="/portal/homework" />
      <nav aria-label="Homework range" className="mb-5 inline-flex rounded-input bg-surface-2 p-1">
        {(["open", "all"] as const).map((v) => (
          <Link
            key={v}
            href={v === "all" ? `${base}&view=all` : base}
            aria-current={(v === "all") === showAll ? "page" : undefined}
            className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", (v === "all") === showAll ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
          >
            {v === "all" ? "Last 60 days" : "Due now"}
          </Link>
        ))}
      </nav>
      {!list.ok ? (
        <Card>
          <ProblemState problem={list.problem} />
        </Card>
      ) : list.data.length === 0 ? (
        <Card>
          <EmptyState icon={ClipboardList} title={showAll ? "No homework in the last 60 days" : "Nothing due right now"}>
            {showAll ? undefined : "New homework shows here and on the bell."}
          </EmptyState>
        </Card>
      ) : (
        <ul className="space-y-3">
          {list.data.map((m) => (
            <li key={m.homework.id}>
              <Link href={`/portal/homework/${m.homework.id}?child=${child.studentId}`} className="block rounded-card border border-border bg-surface p-4 transition-shadow hover:shadow-low">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="min-w-0 flex-1 font-medium">{m.homework.title}</span>
                  {m.homework.acceptsSubmissions && <Badge tone={stateTone[m.state]}>{stateLabel[m.state]}</Badge>}
                </div>
                <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted">
                  <span>{m.homework.subject ?? "General"}</span>
                  <span className={cn(m.isOverdue && "font-medium text-danger")}>{m.isOverdue ? `Overdue · ${dueLabel(m.homework.dueOn, today)}` : dueLabel(m.homework.dueOn, today)}</span>
                  {m.homework.files.length > 0 && (
                    <span className="inline-flex items-center gap-1">
                      <Paperclip className="size-3.5" aria-hidden />
                      {m.homework.files.length}
                    </span>
                  )}
                </p>
                {m.submission?.feedback && <p className="mt-2 rounded-input bg-surface-2 px-3 py-2 text-sm">Teacher: {m.submission.feedback}</p>}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
