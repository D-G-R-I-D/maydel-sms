import type { Metadata } from "next";
import Link from "next/link";
import { Eye } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { SettingsEditor } from "@/components/assessment/settings-editor";
import { TraitsEditor } from "@/components/assessment/traits-editor";

export const metadata: Metadata = { title: "Assessment settings" };

const tabs = [
  { key: "scores", label: "Scores & grading" },
  { key: "behaviour", label: "Behaviour traits" },
] as const;

export default async function AssessmentSettingsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const header = <PageHeader title="Assessment" description="Score components, the grading scale and behaviour traits used on report cards." />;
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
  const sp = await searchParams;
  const tab = (Array.isArray(sp.tab) ? sp.tab[0] : sp.tab) === "behaviour" ? "behaviour" : "scores";

  const nav = (
    <nav aria-label="Assessment settings" className="mb-5 inline-flex rounded-input bg-surface-2 p-1">
      {tabs.map((t) => (
        <Link
          key={t.key}
          href={t.key === "scores" ? "/admin/settings/assessment" : "/admin/settings/assessment?tab=behaviour"}
          aria-current={tab === t.key ? "page" : undefined}
          className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", tab === t.key ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
        >
          {t.label}
        </Link>
      ))}
    </nav>
  );

  const viewOnly = !isAdmin && (
    <p className="-mt-3 mb-5 flex items-center gap-1.5 text-sm text-muted">
      <Eye className="size-4" aria-hidden />
      View only. Ask an administrator to change these.
    </p>
  );

  if (tab === "behaviour") {
    const behaviour = await load((api) => api.GET("/api/assessment/behaviour"));
    return (
      <>
        {header}
        {viewOnly}
        {nav}
        {behaviour.ok ? (
          <TraitsEditor initial={behaviour.data} canEdit={isAdmin} />
        ) : (
          <Card>
            <ProblemState problem={behaviour.problem} />
          </Card>
        )}
      </>
    );
  }

  const [settings, presets] = await Promise.all([
    load((api) => api.GET("/api/assessment/settings")),
    isAdmin ? load((api) => api.GET("/api/assessment/grading-presets")) : Promise.resolve(null),
  ]);
  return (
    <>
      {header}
      {viewOnly}
      {nav}
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
