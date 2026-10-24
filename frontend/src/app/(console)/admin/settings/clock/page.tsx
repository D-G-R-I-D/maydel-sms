import type { Metadata } from "next";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { ClockSettingsForm, KioskLinkCard } from "@/components/clock/clock-settings-form";
import { publicSiteOrigin } from "@/lib/clock/origin";

export const metadata: Metadata = { title: "Staff clock-in" };

/** Settings → Staff clock-in (admin): school hours, grace, the location check and the kiosk link. */
export default async function ClockSettingsPage() {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="Staff clock-in" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const [settings, link] = await Promise.all([load((api) => api.GET("/api/clock/settings")), load((api) => api.GET("/api/clock/kiosk-link"))]);
  // 404 clock.no_kiosk: no link yet, or one issued before links were kept (the card offers to issue).
  const token = link.ok ? link.data.token : null;
  return (
    <>
      <PageHeader title="Staff clock-in" description="Staff scan a QR code on a screen at the entrance when they arrive and leave. Times are Abuja time (WAT)." />
      {settings.ok ? (
        <div className="grid gap-5 lg:grid-cols-[3fr_2fr] lg:items-start">
          <ClockSettingsForm initial={settings.data} />
          <KioskLinkCard token={token} issued={settings.data.kioskLinkIssued} publicOrigin={publicSiteOrigin()} />
        </div>
      ) : (
        <Card>
          <ProblemState problem={settings.problem} />
        </Card>
      )}
    </>
  );
}
