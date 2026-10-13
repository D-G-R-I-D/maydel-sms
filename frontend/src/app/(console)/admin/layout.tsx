import type { Metadata } from "next";
import { Suspense } from "react";
import { requireSessionUser } from "@/lib/auth/session";
import { CurrentTermChip, CurrentTermChipFallback } from "@/components/shell/current-term-chip";
import { CommandPalette } from "@/components/shell/command-palette";
import { ConsoleSidebar } from "@/components/shell/console-sidebar";
import { consoleNav, filterNav } from "@/components/shell/nav";
import { UserMenu } from "@/components/shell/user-menu";

export const metadata: Metadata = { title: { default: "Console", template: "%s · Maydel console" } };

export default async function ConsoleLayout({ children }: { children: React.ReactNode }) {
  const user = await requireSessionUser();
  const groups = filterNav(consoleNav, user.roles);

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
          <div className="mx-auto w-full max-w-6xl">{children}</div>
        </main>
      </div>
    </div>
  );
}
