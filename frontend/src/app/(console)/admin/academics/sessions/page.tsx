import type { Metadata } from "next";
import { CalendarDays, CalendarRange, Coffee, Eye } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import type { TermDto } from "@/lib/api/types";
import { formatRange, nextSessionName, nextTermNumber } from "@/lib/validation/academics";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { AddTermButton, EditSessionButton, EditTermButton, MakeCurrentButton, NewSessionButton } from "@/components/academics/session-forms";

export const metadata: Metadata = { title: "Sessions & terms" };

function CurrentBadge() {
  return (
    <Badge tone="success">
      <span className="size-1.5 rounded-full bg-success" aria-hidden />
      Current
    </Badge>
  );
}

export default async function SessionsPage() {
  const user = await requireSessionUser();
  const header = (actions?: React.ReactNode) => (
    <PageHeader title="Sessions & terms" description="The school calendar: each session (school year) and its three terms." actions={actions} />
  );
  if (!hasAnyRole(user.roles, ["Admin", "Teacher", "Bursar"])) {
    return (
      <>
        {header()}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const sessions = await load((api) => api.GET("/api/academics/sessions"));
  if (!sessions.ok) {
    return (
      <>
        {header()}
        <Card>
          <ProblemState problem={sessions.problem} />
        </Card>
      </>
    );
  }

  const list = sessions.data;
  const currentTerm: TermDto | undefined = list.flatMap((s) => s.terms).find((t) => t.isCurrent);
  const suggested = nextSessionName(list[0]?.name);

  return (
    <>
      {header(isAdmin ? <NewSessionButton suggestedName={suggested} /> : undefined)}
      {!isAdmin && (
        <p className="-mt-3 mb-5 flex items-center gap-1.5 text-sm text-muted">
          <Eye className="size-4" aria-hidden />
          View only. Ask an administrator to change the calendar.
        </p>
      )}

      {list.length === 0 ? (
        <Card>
          <EmptyState icon={CalendarRange} title="No sessions yet" action={isAdmin ? <NewSessionButton suggestedName={suggested} /> : undefined}>
            {isAdmin ? "Create the school year, then add its First, Second and Third terms." : "An administrator hasn't set up the school calendar yet."}
          </EmptyState>
        </Card>
      ) : (
        <ol className="space-y-5">
          {list.map((session, i) => {
            const next = nextTermNumber(session.terms.map((t) => t.number));
            return (
              <li key={session.id} className={cn("animate-rise", ["", "delay-1", "delay-2", "delay-3"][Math.min(i, 3)])}>
                <Card className={cn("overflow-hidden", session.isCurrent && "border-brand/40")}>
                  <div className={cn("flex flex-wrap items-center gap-x-4 gap-y-2 px-5 py-4", session.isCurrent ? "bg-brand/5" : "bg-surface")}>
                    <span className="inline-flex size-10 items-center justify-center rounded-full bg-brand/10 text-brand">
                      <CalendarDays className="size-5" aria-hidden />
                    </span>
                    <div className="min-w-0 flex-1">
                      <h2 className="flex flex-wrap items-center gap-2 font-display text-xl font-semibold">
                        {session.name}
                        {session.isCurrent && <CurrentBadge />}
                      </h2>
                      <p className="tabular text-sm text-muted">{formatRange(session.startsOn, session.endsOn)}</p>
                    </div>
                    {isAdmin && <EditSessionButton session={session} />}
                  </div>

                  {session.terms.length === 0 ? (
                    <p className="border-t border-border px-5 py-4 text-sm text-muted">No terms yet.</p>
                  ) : (
                    <ul className="divide-y divide-border border-t border-border">
                      {session.terms.map((term) => (
                        <li key={term.id} className={cn("flex flex-wrap items-center gap-x-4 gap-y-2 px-5 py-3", term.isCurrent && "bg-success/5")}>
                          <div className="min-w-0 flex-1">
                            <p className="flex flex-wrap items-center gap-2 font-medium">
                              {term.number} Term
                              {term.isCurrent && <CurrentBadge />}
                            </p>
                            <p className="tabular text-sm text-muted">{formatRange(term.startsOn, term.endsOn)}</p>
                            {term.midTermStartsOn && term.midTermEndsOn && (
                              <p className="tabular mt-0.5 flex items-center gap-1.5 text-xs text-muted">
                                <Coffee className="size-3.5" aria-hidden />
                                Mid-term break {formatRange(term.midTermStartsOn, term.midTermEndsOn)}
                              </p>
                            )}
                          </div>
                          {isAdmin && (
                            <div className="flex items-center gap-1">
                              {!term.isCurrent && <MakeCurrentButton term={term} current={currentTerm?.name} />}
                              <EditTermButton session={session} term={term} />
                            </div>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}

                  {isAdmin && next && (
                    <div className="border-t border-border px-5 py-3">
                      <AddTermButton session={session} number={next} />
                    </div>
                  )}
                </Card>
              </li>
            );
          })}
        </ol>
      )}
    </>
  );
}
