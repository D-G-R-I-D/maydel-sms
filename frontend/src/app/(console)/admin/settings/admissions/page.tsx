import type { Metadata } from "next";
import { Eye } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadAdmissionSessions } from "@/lib/admissions/sessions";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { SessionPicker } from "@/components/admissions/session-picker";
import { AdmissionSettingsForm } from "@/components/admissions/admission-settings-form";

export const metadata: Metadata = { title: "Admissions settings" };

/** E11.4: open/close admissions per session, the form fee, the exam window and instructions. */
export default async function AdmissionSettingsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Admissions" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const sp = await searchParams;
  const { sessions, selected } = await loadAdmissionSessions(Array.isArray(sp.sessionId) ? sp.sessionId[0] : sp.sessionId);
  const header = (
    <PageHeader
      title="Admissions"
      description="Open or close admissions for a session, and set the form fee and how long families have to book the entrance exam."
      actions={selected ? <SessionPicker sessions={sessions} value={selected.id} /> : undefined}
    />
  );
  if (!selected) {
    return (
      <>
        {header}
        <Card>
          <EmptyState title="No sessions yet">Set up the school calendar under Academics first.</EmptyState>
        </Card>
      </>
    );
  }
  const settings = await load((api) => api.GET("/api/admissions/settings", { params: { query: { sessionId: selected.id } } }));
  return (
    <>
      {header}
      {!isAdmin && (
        <p className="-mt-3 mb-5 flex items-center gap-1.5 text-sm text-muted">
          <Eye className="size-4" aria-hidden />
          View only. Ask an administrator to change these.
        </p>
      )}
      {settings.ok ? (
        <AdmissionSettingsForm key={selected.id} initial={settings.data} canEdit={isAdmin} />
      ) : (
        <Card>
          <ProblemState problem={settings.problem} />
        </Card>
      )}
    </>
  );
}
