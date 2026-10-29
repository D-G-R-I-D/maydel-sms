import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight, BookOpenCheck, CalendarCheck, ClipboardList, NotebookPen, ShoppingBag, Users, Wallet, type LucideIcon } from "lucide-react";
import { formatNaira } from "@/lib/finance/rules";
import { load } from "@/lib/api/server";
import { firstName } from "@/lib/flash";
import { formatAverage, ordinal } from "@/lib/reports/rules";
import { Card, cn } from "@/components/ui/primitives";
import { WelcomeBand } from "@/components/ui/welcome-band";
import { childAccent } from "@/lib/portal/accent";
import type { MyHomework } from "@/lib/homework/homework";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { PhotoAvatar } from "@/components/media/photo-avatar";

export const metadata: Metadata = { title: "Home" };

const later: { icon: LucideIcon; title: string; body: string }[] = [{ icon: CalendarCheck, title: "Attendance", body: "Daily attendance will appear here." }];

/** Parent/student home: one card per child with their latest published result (design system §5). */
export default async function PortalHomePage() {
  const [me, children] = await Promise.all([load((api) => api.GET("/api/auth/me")), load((api) => api.GET("/api/portal/children"))]);
  if (!me.ok) return <ProblemState problem={me.problem} />;
  const name = firstName(me.data.fullName) || me.data.fullName;
  // Each child's fee totals, for a one-line summary on their card (E6.3).
  const fees = children.ok
    ? new Map(
        await Promise.all(
          children.data.map(async (c) => [c.studentId, await load((api) => api.GET("/api/portal/children/{studentId}/fees", { params: { path: { studentId: c.studentId } } }))] as const),
        ),
      )
    : new Map();
  // Homework due now, per child, for a nudge on their card (E17.1).
  const homework = children.ok
    ? new Map(
        await Promise.all(
          children.data.map(
            async (c) => [c.studentId, await load((api) => api.GET("/api/portal/children/{studentId}/homework", { params: { path: { studentId: c.studentId }, query: { open: true } } }))] as const,
          ),
        ),
      )
    : new Map();
  // Communication books (E17.2): unread notes per child.
  const books = await load((api) => api.GET("/api/portal/books"));
  const unreadFor = (studentId: string) => (books.ok ? (books.data.find((b) => b.studentId === studentId)?.unread ?? 0) : 0);
  const isParent = me.data.roles.includes("Parent");
  const today = new Intl.DateTimeFormat("en-NG", { weekday: "long", day: "numeric", month: "long", timeZone: "Africa/Lagos" }).format(new Date());

  return (
    <>
      <WelcomeBand eyebrow={today} title="Welcome," accent={name}>
        {isParent ? "Everything about your children at Maydel, in one place." : "Your homework, results and school news."}
      </WelcomeBand>

      {!children.ok ? (
        <Card>
          <ProblemState problem={children.problem} />
        </Card>
      ) : children.data.length === 0 ? (
        <Card>
          <EmptyState icon={Users} title={isParent ? "No children linked yet" : "Nothing to show yet"}>
            {isParent ? "Ask the school office to link your children to your account." : "Your results will appear here once the school releases them."}
          </EmptyState>
        </Card>
      ) : (
        <ul className="grid gap-4 md:grid-cols-2">
          {children.data.map((c, i) => {
            const r = c.latestResult;
            const accent = childAccent(i);
            const hw = homework.get(c.studentId);
            const due = hw?.ok ? hw.data.filter((m: MyHomework) => m.state === "Missing" || m.state === "Returned") : [];
            const overdue = due.filter((m: MyHomework) => m.isOverdue).length;
            return (
              <li key={c.studentId} className={["animate-rise", "animate-rise delay-1", "animate-rise delay-2"][Math.min(i, 2)]}>
                <Card className={cn("card-interactive h-full border-l-4 p-5", accent.border)}>
                  <div className="flex items-center gap-3">
                    <PhotoAvatar kind="portal" id={c.studentId} name={c.name} photoVersion={c.photoVersion} className={cn("size-14 text-lg ring-2 ring-offset-2 ring-offset-surface", accent.ring)} />
                    <div className="min-w-0">
                      <h2 className="truncate font-display text-lg font-semibold">{c.name}</h2>
                      <p className="text-sm text-muted">{c.classRoom ?? "No class yet"}</p>
                    </div>
                  </div>
                  {r ? (
                    <Link href={`/portal/results/${c.studentId}/${r.termId}`} className="group mt-4 flex items-center gap-3 rounded-input p-3 transition-colors hover:bg-surface-2">
                      <IconChip icon={ClipboardList} tone="bg-brand/10 text-brand" />
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm font-medium">
                          {r.term} {r.session}
                        </span>
                        <span className="tabular block text-sm text-muted">
                          Average {formatAverage(r.average)}
                          {r.position ? ` · ${ordinal(r.position)} of ${r.classSize}` : ""}
                        </span>
                      </span>
                      <ArrowRight className="size-4 text-muted transition-transform group-hover:translate-x-0.5 group-hover:text-brand" aria-hidden />
                    </Link>
                  ) : (
                    <p className="mt-4 rounded-input bg-surface-2 p-3 text-sm text-muted">No results published yet. You&apos;ll see them here once the school releases them.</p>
                  )}
                  {(() => {
                    const f = fees.get(c.studentId);
                    if (!f?.ok || f.data.invoices.length === 0) return null;
                    return (
                      <Link href={`/portal/fees?child=${c.studentId}`} className="group mt-2 flex items-center gap-3 rounded-input p-3 transition-colors hover:bg-surface-2">
                        <IconChip icon={Wallet} tone={f.data.overdue > 0 ? "bg-danger/10 text-danger" : f.data.balance > 0 ? "bg-warning/15 text-warning" : "bg-success/15 text-success"} />
                        <span className="tabular min-w-0 flex-1 text-sm">
                          Balance {formatNaira(f.data.balance)}
                          {f.data.overdue > 0 && <span className="text-danger"> · Overdue {formatNaira(f.data.overdue)}</span>}
                        </span>
                        <ArrowRight className="size-4 text-muted transition-transform group-hover:translate-x-0.5 group-hover:text-brand" aria-hidden />
                      </Link>
                    );
                  })()}
                  {books.ok && (
                    <Link href={`/portal/book/${c.studentId}`} className="group mt-2 flex items-center gap-3 rounded-input p-3 transition-colors hover:bg-surface-2">
                      <IconChip icon={NotebookPen} tone={unreadFor(c.studentId) > 0 ? "bg-brand/10 text-brand" : "bg-surface-2 text-muted"} />
                      <span className="min-w-0 flex-1 text-sm">
                        Communication book
                        {unreadFor(c.studentId) > 0 && <span className="font-medium text-brand"> · {unreadFor(c.studentId)} new</span>}
                      </span>
                      <ArrowRight className="size-4 text-muted transition-transform group-hover:translate-x-0.5 group-hover:text-brand" aria-hidden />
                    </Link>
                  )}
                  {hw?.ok && (
                    <Link href={`/portal/homework?child=${c.studentId}`} className="group mt-2 flex items-center gap-3 rounded-input p-3 transition-colors hover:bg-surface-2">
                      <IconChip icon={BookOpenCheck} tone={overdue > 0 ? "bg-danger/10 text-danger" : due.length > 0 ? "bg-brand/10 text-brand" : "bg-success/15 text-success"} />
                      <span className="min-w-0 flex-1 text-sm">
                        {due.length === 0 ? "No homework due" : `${due.length} homework to hand in`}
                        {overdue > 0 && <span className="text-danger"> · {overdue} overdue</span>}
                      </span>
                      <ArrowRight className="size-4 text-muted transition-transform group-hover:translate-x-0.5 group-hover:text-brand" aria-hidden />
                    </Link>
                  )}
                  {isParent && (
                    <Link href={`/portal/shop?child=${c.studentId}`} className="group mt-2 flex items-center gap-3 rounded-input p-3 transition-colors hover:bg-surface-2">
                      <IconChip icon={ShoppingBag} tone="bg-surface-2 text-muted" />
                      <span className="min-w-0 flex-1 text-sm">School shop: uniforms and books</span>
                      <ArrowRight className="size-4 text-muted transition-transform group-hover:translate-x-0.5 group-hover:text-brand" aria-hidden />
                    </Link>
                  )}
                </Card>
              </li>
            );
          })}
        </ul>
      )}

      <div className="mt-6 grid gap-4 sm:grid-cols-2">
        {later.map(({ icon: Icon, title, body }) => (
          <Card key={title} className="flex items-start gap-3 p-4 opacity-80">
            <Icon className="mt-0.5 size-5 text-muted" aria-hidden />
            <div>
              <p className="font-medium">
                {title} <span className="text-xs font-normal text-muted">· coming soon</span>
              </p>
              <p className="text-sm text-muted">{body}</p>
            </div>
          </Card>
        ))}
      </div>
    </>
  );
}

/** A small tinted circle behind an icon: brand for information, green for done, amber/red for attention. */
function IconChip({ icon: Icon, tone }: { icon: LucideIcon; tone: string }) {
  return (
    <span className={cn("inline-flex size-9 shrink-0 items-center justify-center rounded-full", tone)}>
      <Icon className="size-[1.1rem]" aria-hidden />
    </span>
  );
}
