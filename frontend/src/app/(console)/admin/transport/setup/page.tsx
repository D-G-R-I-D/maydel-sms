import type { Metadata } from "next";
import { Bus, Pencil, Plus } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Badge, Button, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { BusDialog, QuestionsEditor, RunsEditor } from "@/components/transport/transport-setup";
import { TransportTabs } from "@/components/transport/transport-tabs";

export const metadata: Metadata = { title: "Transport setup" };

/** E18 admin: buses and their drivers, the runs, and the drivers' daily checklist. */
export default async function TransportSetupPage() {
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
  const [buses, runs, questions, staff] = await Promise.all([
    load((api) => api.GET("/api/transport/buses")),
    load((api) => api.GET("/api/transport/runs")),
    load((api) => api.GET("/api/transport/questions")),
    load((api) => api.GET("/api/staff/lookup")),
  ]);
  const people = staff.ok ? staff.data : [];
  const add = (
    <BusDialog
      bus={null}
      staff={people}
      trigger={
        <Button>
          <Plus className="size-4" aria-hidden />
          Add a bus
        </Button>
      }
    />
  );
  // Remount the list editors when the saved data changes, so new rows pick up their server ids.
  const runsKey = runs.ok ? runs.data.map((r) => `${r.id}:${r.name}:${r.order}:${r.isActive}`).join("|") : "";
  const questionsKey = questions.ok ? questions.data.map((q) => `${q.id}:${q.text}:${q.order}:${q.isActive}:${q.kind}:${q.expectedYes}:${q.flagIfAnswered}`).join("|") : "";

  return (
    <>
      <PageHeader title="Transport" description="Buses and drivers, the daily runs, and the drivers' checklist." />
      <TransportTabs active="setup" />
      <div className="space-y-5">
        <Card className="overflow-hidden">
          <div className="flex flex-wrap items-center gap-3 border-b border-border p-5">
            <div className="min-w-0 flex-1">
              <h2 className="font-display text-lg font-semibold">Buses</h2>
              <p className="text-sm text-muted">Each driver sees only their own bus on their phone.</p>
            </div>
            {buses.ok && buses.data.length > 0 && add}
          </div>
          {!buses.ok ? (
            <ProblemState problem={buses.problem} />
          ) : buses.data.length === 0 ? (
            <EmptyState icon={Bus} title="No buses yet" action={add} />
          ) : (
            <ul className="divide-y divide-border">
              {buses.data.map((b) => (
                <li key={b.id} className="flex items-center gap-3 px-5 py-3">
                  <Bus className="size-5 shrink-0 text-brand" aria-hidden />
                  <span className="min-w-0 flex-1">
                    <span className="font-medium">{b.name}</span>
                    {b.plateNumber && <span className="ml-2 text-sm text-muted">{b.plateNumber}</span>}
                    <span className="block text-xs text-muted">
                      {b.capacity} seats · {b.driverName ?? "No driver"}
                    </span>
                  </span>
                  {!b.isActive && <Badge>Retired</Badge>}
                  <BusDialog
                    bus={b}
                    staff={people}
                    trigger={
                      <Button variant="ghost" size="sm" aria-label={`Edit ${b.name}`}>
                        <Pencil className="size-4" aria-hidden />
                      </Button>
                    }
                  />
                </li>
              ))}
            </ul>
          )}
        </Card>
        <div className="grid gap-5 xl:grid-cols-2">
          {runs.ok ? (
            <RunsEditor key={runsKey} initial={runs.data} />
          ) : (
            <Card>
              <ProblemState problem={runs.problem} />
            </Card>
          )}
          {questions.ok ? (
            <QuestionsEditor key={questionsKey} initial={questions.data} />
          ) : (
            <Card>
              <ProblemState problem={questions.problem} />
            </Card>
          )}
        </div>
      </div>
    </>
  );
}
