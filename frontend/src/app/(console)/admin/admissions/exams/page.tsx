import type { Metadata } from "next";
import Link from "next/link";
import { CalendarClock, ChevronRight } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadAdmissionSessions } from "@/lib/admissions/sessions";
import { formatSlotStart, modeLabel } from "@/lib/admissions/exam";
import { Card, PageHeader } from "@/components/ui/primitives";
import { CapacityBar } from "@/components/academics/capacity-bar";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { SessionPicker } from "@/components/admissions/session-picker";
import { AdmissionsTabs } from "@/components/admissions/admissions-tabs";
import { AddExamSlotButton, ExamSlotActions } from "@/components/admissions/exam-slot-form";

export const metadata: Metadata = { title: "Exam sittings" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E11.6: entrance exam sittings. Admin manages them; teachers open them to enter scores. */
export default async function ExamSittingsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Teacher", "Bursar"])) {
    return (
      <>
        <PageHeader title="Exam sittings" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const sp = await searchParams;
  const { sessions, selected } = await loadAdmissionSessions(one(sp.sessionId));
  if (!selected) {
    return (
      <>
        <PageHeader title="Exam sittings" />
        <Card>
          <EmptyState title="No sessions yet">Set up the school calendar under Academics first.</EmptyState>
        </Card>
      </>
    );
  }
  const slots = await load((api) => api.GET("/api/admissions/exam-slots", { params: { query: { sessionId: selected.id } } }));

  return (
    <>
      <PageHeader
        title="Entrance exams"
        description="Sittings families can book once they've submitted the form. Open a sitting for its attendance sheet and to enter scores."
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <SessionPicker sessions={sessions} value={selected.id} />
            {isAdmin && <AddExamSlotButton sessionId={selected.id} />}
          </div>
        }
      />
      <AdmissionsTabs active="exams" sessionId={selected.id} isAdmin={isAdmin} isBursar={hasAnyRole(user.roles, ["Bursar"])} />
      <Card className="overflow-hidden">
        {!slots.ok ? (
          <ProblemState problem={slots.problem} />
        ) : slots.data.length === 0 ? (
          <EmptyState icon={CalendarClock} title="No sittings yet">
            {isAdmin ? "Add the dates and times families can choose from." : "An administrator will add the exam times."}
          </EmptyState>
        ) : (
          <ul className="divide-y divide-border">
            {slots.data.map((s) => {
              return (
                <li key={s.id} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-5 py-3">
                  <Link href={`/admin/admissions/exams/${s.id}?sessionId=${selected.id}`} className="group min-w-0 flex-1 basis-60">
                    <span className="flex items-center gap-1 font-medium group-hover:text-brand">
                      {formatSlotStart(s.startsAt)}
                      <ChevronRight className="size-4 text-muted" aria-hidden />
                    </span>
                    <span className="block text-sm text-muted">
                      {s.durationMinutes} min · {s.venue} · {modeLabel[s.mode]}
                    </span>
                  </Link>
                  <div className="w-40">
                    <CapacityBar count={s.booked} capacity={s.capacity} />
                  </div>
                  {isAdmin && <ExamSlotActions sessionId={selected.id} slot={s} />}
                </li>
              );
            })}
          </ul>
        )}
      </Card>
    </>
  );
}
