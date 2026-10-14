import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight, CalendarCheck, ClipboardList, Users, Wallet, type LucideIcon } from "lucide-react";
import { load } from "@/lib/api/server";
import { firstName } from "@/lib/flash";
import { formatAverage, ordinal } from "@/lib/reports/rules";
import { Card, PageHeader } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";
import { PhotoAvatar } from "@/components/media/photo-avatar";

export const metadata: Metadata = { title: "Home" };

const later: { icon: LucideIcon; title: string; body: string }[] = [
  { icon: Wallet, title: "Fees", body: "Invoices and receipts will appear here." },
  { icon: CalendarCheck, title: "Attendance", body: "Daily attendance will appear here." },
];

/** Parent/student home: one card per child with their latest published result (design system §5). */
export default async function PortalHomePage() {
  const [me, children] = await Promise.all([load((api) => api.GET("/api/auth/me")), load((api) => api.GET("/api/portal/children"))]);
  if (!me.ok) return <ProblemState problem={me.problem} />;
  const name = firstName(me.data.fullName) || me.data.fullName;
  const isParent = me.data.roles.includes("Parent");

  return (
    <>
      <PageHeader title={`Welcome, ${name}`} description={isParent ? "Everything about your children at Maydel, in one place." : "Your results and school news."} />

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
            return (
              <li key={c.studentId} className={["animate-rise", "animate-rise delay-1", "animate-rise delay-2"][Math.min(i, 2)]}>
                <Card className="h-full p-5">
                  <div className="flex items-center gap-3">
                    <PhotoAvatar kind="portal" id={c.studentId} name={c.name} photoVersion={c.photoVersion} className="size-14 text-lg" />
                    <div className="min-w-0">
                      <h2 className="truncate font-display text-lg font-semibold">{c.name}</h2>
                      <p className="text-sm text-muted">{c.classRoom ?? "No class yet"}</p>
                    </div>
                  </div>
                  {r ? (
                    <Link href={`/portal/results/${c.studentId}/${r.termId}`} className="group mt-4 flex items-center gap-3 rounded-input bg-brand/5 p-3 transition-colors hover:bg-brand/10">
                      <ClipboardList className="size-5 shrink-0 text-brand" aria-hidden />
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
