import type { Metadata } from "next";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { StoreTabs } from "@/components/store/store-tabs";
import { StoreSettingsForm } from "@/components/store/store-settings-form";

export const metadata: Metadata = { title: "Store settings" };

/** E19: parent shop rules. */
export default async function StoreSettingsPage() {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Store settings" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const settings = await load((api) => api.GET("/api/store/settings"));
  return (
    <>
      <PageHeader title="School store" description="How the parents' shop works." />
      <StoreTabs active="settings" />
      {settings.ok ? (
        <StoreSettingsForm initial={settings.data} />
      ) : (
        <Card>
          <ProblemState problem={settings.problem} />
        </Card>
      )}
    </>
  );
}
