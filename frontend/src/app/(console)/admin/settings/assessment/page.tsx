import type { Metadata } from "next";
import { Eye } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { SettingsEditor } from "@/components/assessment/settings-editor";

export const metadata: Metadata = { title: "Assessment settings" };

export default async function AssessmentSettingsPage() {
  const user = await requireSessionUser();
  const header = <PageHeader title="Assessment" description="Score components and the grading scale used on report cards." />;
  if (!hasAnyRole(user.roles, ["Admin", "Teacher"])) {
    return (
      <>
        {header}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const [settings, presets] = await Promise.all([
    load((api) => api.GET("/api/assessment/settings")),
    isAdmin ? load((api) => api.GET("/api/assessment/grading-presets")) : Promise.resolve(null),
  ]);
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
        <SettingsEditor initial={settings.data} canEdit={isAdmin} presets={presets?.ok ? presets.data : []} />
      ) : (
        <Card>
          <ProblemState problem={settings.problem} />
        </Card>
      )}
    </>
  );
}
