import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import { requireSessionUser } from "@/lib/auth/session";
import { CurrentTermChip, CurrentTermChipFallback } from "@/components/shell/current-term-chip";
import { portalNav } from "@/components/shell/nav";
import { PortalNav } from "@/components/shell/portal-nav";
import { SchoolLogo } from "@/components/school/school-logo";
import { UserMenu } from "@/components/shell/user-menu";
import { NotificationBell } from "@/components/shell/notification-bell";
import { loadBell } from "@/lib/notifications/actions";
import { NewsletterNudge } from "@/components/site/newsletter-nudge";

export const metadata: Metadata = { title: { default: "Portal", template: "%s · Maydel portal" } };

export default async function PortalLayout({ children }: { children: React.ReactNode }) {
  const user = await requireSessionUser();
  const bell = await loadBell();
  const items = portalNav.filter((i) => i.roles.some((r) => user.roles.includes(r)));

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-20 border-b border-border bg-surface/90 backdrop-blur">
        <div className="mx-auto flex h-16 w-full max-w-5xl items-center justify-between gap-4 px-4 sm:px-6">
          <Link href="/portal" className="flex shrink-0 items-center gap-2.5">
            <SchoolLogo width={28} height={35} />
            <span className="font-display text-base font-semibold text-brand">Maydel</span>
          </Link>
          <PortalNav items={items} variant="top" />
          <div className="flex shrink-0 items-center gap-2">
            <span className="hidden sm:inline-flex lg:hidden">
              <Suspense fallback={<CurrentTermChipFallback />}>
                <CurrentTermChip />
              </Suspense>
            </span>
            <NotificationBell initial={bell} noticesHref="/portal/notices" />
            <UserMenu fullName={user.fullName} userName={user.userName} roles={user.roles} />
          </div>
        </div>
      </header>
      <main id="main" className="mx-auto w-full max-w-5xl flex-1 px-4 pt-6 pb-24 sm:px-6 lg:pb-10">
        {children}
      </main>
      <PortalNav items={items} variant="bottom" />
      <NewsletterNudge />
    </div>
  );
}
