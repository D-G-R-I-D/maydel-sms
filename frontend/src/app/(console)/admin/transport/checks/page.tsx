import type { Metadata } from "next";
import Link from "next/link";
import { ClipboardCheck } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { addDays, shortDay, todayWat } from "@/lib/clock/clock";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { CheckAnswers } from "@/components/transport/check-answers";
import { TransportTabs } from "@/components/transport/transport-tabs";

export const metadata: Metadata = { title: "Bus checklists" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E18 admin: drivers' daily checklists for the last 30 days, newest first; optionally only flagged ones. */
export default async function TransportChecksPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Transport" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const flaggedOnly = one((await searchParams).view) === "flagged";
  const to = todayWat();
  const checks = await load((api) => api.GET("/api/transport/checks", { params: { query: { from: addDays(to, -29), to, ...(flaggedOnly ? { flaggedOnly: true } : {}) } } }));

  return (
    <>
      <PageHeader title="Transport" description="The drivers' daily safety checklists for the last 30 days." />
      <TransportTabs active="checks" />
      <nav aria-label="Checklist filter" className="mb-5 inline-flex rounded-input bg-surface-2 p-1">
        {(["all", "flagged"] as const).map((v) => (
          <Link
            key={v}
            href={v === "flagged" ? "/admin/transport/checks?view=flagged" : "/admin/transport/checks"}
            aria-current={(v === "flagged") === flaggedOnly ? "page" : undefined}
            className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", (v === "flagged") === flaggedOnly ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
          >
            {v === "flagged" ? "Flagged only" : "All"}
          </Link>
        ))}
      </nav>
      {!checks.ok ? (
        <Card>
          <ProblemState problem={checks.problem} />
        </Card>
      ) : checks.data.length === 0 ? (
        <Card>
          <EmptyState icon={ClipboardCheck} title={flaggedOnly ? "Nothing flagged in the last 30 days" : "No checklists yet"}>
            {flaggedOnly ? "Good news." : "Drivers fill one in each morning on their phone."}
          </EmptyState>
        </Card>
      ) : (
        <ul className="grid gap-4 lg:grid-cols-2">
          {checks.data.map((c) => (
            <li key={c.id}>
              <Card className={cn("space-y-3 p-5", c.isFlagged && "border-warning/50")}>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-display text-lg font-semibold">{c.bus}</span>
                  <span className="text-sm text-muted">{shortDay(c.date)}</span>
                  {c.isFlagged && (
                    <Badge tone="warning" className="ml-auto">
                      Flagged
                    </Badge>
                  )}
                </div>
                <CheckAnswers check={c} />
              </Card>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
