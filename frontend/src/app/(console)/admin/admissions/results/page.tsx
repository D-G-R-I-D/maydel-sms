import type { Metadata } from "next";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadAdmissionSessions } from "@/lib/admissions/sessions";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { SessionPicker } from "@/components/admissions/session-picker";
import { AdmissionsTabs } from "@/components/admissions/admissions-tabs";
import { ResultsView } from "@/components/admissions/results-view";

export const metadata: Metadata = { title: "Entrance exam results" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E11.7–E11.8 (admin): cut-off, pass/fail lists, offers and publishing decisions. */
export default async function ResultsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Entrance exam results" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const { sessions, selected } = await loadAdmissionSessions(one(sp.sessionId));
  if (!selected) {
    return (
      <>
        <PageHeader title="Entrance exam results" />
        <Card>
          <EmptyState title="No sessions yet">Set up the school calendar under Academics first.</EmptyState>
        </Card>
      </>
    );
  }
  const results = await load((api) => api.GET("/api/admissions/results", { params: { query: { sessionId: selected.id } } }));
  return (
    <>
      <PageHeader
        title="Admissions"
        description="Set the cut-off, tick who to offer a place, then publish. Families see nothing until you publish."
        actions={<SessionPicker sessions={sessions} value={selected.id} />}
      />
      <AdmissionsTabs active="results" sessionId={selected.id} isAdmin isBursar={hasAnyRole(user.roles, ["Bursar"])} />
      {results.ok ? (
        <ResultsView key={selected.id} sessionId={selected.id} initial={results.data} csvHref={`/api/media/admissions/results-csv?sessionId=${selected.id}`} />
      ) : (
        <Card>
          <ProblemState problem={results.problem} />
        </Card>
      )}
    </>
  );
}
