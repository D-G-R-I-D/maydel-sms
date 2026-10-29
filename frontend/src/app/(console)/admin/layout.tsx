import type { Metadata } from "next";
import { Suspense } from "react";
import { requireSessionUser } from "@/lib/auth/session";
import { CurrentTermChip, CurrentTermChipFallback } from "@/components/shell/current-term-chip";
import { CommandPalette } from "@/components/shell/command-palette";
import { ConsoleSidebar } from "@/components/shell/console-sidebar";
import { consoleNav, filterNav, withBadges } from "@/components/shell/nav";
import { load } from "@/lib/api/server";
import { hasAnyRole } from "@/lib/auth/roles";
import { UserMenu } from "@/components/shell/user-menu";
import { DraftScope } from "@/components/drafts/draft-scope";

export const metadata: Metadata = { title: { default: "Console", template: "%s · Maydel console" } };

export default async function ConsoleLayout({ children }: { children: React.ReactNode }) {
  const user = await requireSessionUser();
  // Proofs of payment waiting for the bursar (E10.8): a count on "To verify".
  const [pending, messages] = await Promise.all([
    hasAnyRole(user.roles, ["Admin", "Bursar"]) ? load((api) => api.GET("/api/finance/proofs", { params: { query: { page: 1, pageSize: 1 } } })) : null,
    hasAnyRole(user.roles, ["Admin"]) ? load((api) => api.GET("/api/cms/messages", { params: { query: { status: "New", page: 1, pageSize: 1 } } })) : null,
  ]);
  const groups = withBadges(filterNav(consoleNav, user.roles), {
    "/admin/proofs": pending?.ok ? pending.data.totalCount : 0,
    "/admin/website/messages": messages?.ok ? messages.data.totalCount : 0,
  });

  return (
    <div className="flex min-h-dvh">
      <ConsoleSidebar groups={groups} />
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex h-16 items-center justify-between gap-3 border-b border-border bg-surface/85 pr-4 pl-14 backdrop-blur-md sm:pr-6 lg:pl-6">
          <CommandPalette groups={groups} />
          <div className="flex items-center gap-2">
            <span className="hidden md:inline-flex">
              <Suspense fallback={<CurrentTermChipFallback />}>
                <CurrentTermChip href="/admin/academics/sessions" />
              </Suspense>
            </span>
            <UserMenu fullName={user.fullName} userName={user.userName} roles={user.roles} />
          </div>
        </header>
        <main id="main" className="flex-1 px-4 py-6 sm:px-6 lg:px-8">
          <div className="mx-auto w-full max-w-6xl">
            <DraftScope userId={user.id}>{children}</DraftScope>
          </div>
        </main>
      </div>
    </div>
  );
}
