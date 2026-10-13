import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { requireSessionUser } from "@/lib/auth/session";
import { portalNav } from "@/components/shell/nav";
import { PortalNav } from "@/components/shell/portal-nav";
import { UserMenu } from "@/components/shell/user-menu";

export const metadata: Metadata = { title: { default: "Portal", template: "%s · Maydel portal" } };

export default async function PortalLayout({ children }: { children: React.ReactNode }) {
  const user = await requireSessionUser();
  const items = portalNav.filter((i) => i.roles.some((r) => user.roles.includes(r)));

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-20 border-b border-border bg-surface/90 backdrop-blur">
        <div className="mx-auto flex h-16 w-full max-w-5xl items-center justify-between gap-4 px-4 sm:px-6">
          <Link href="/portal" className="flex items-center gap-2.5">
            <Image src="/brand/crest.svg" alt="" width={28} height={35} />
            <span className="font-display text-base font-semibold text-brand">Maydel</span>
          </Link>
          <PortalNav items={items} variant="top" />
          <UserMenu fullName={user.fullName} userName={user.userName} roles={user.roles} />
        </div>
      </header>
      <main id="main" className="mx-auto w-full max-w-5xl flex-1 px-4 pt-6 pb-24 sm:px-6 md:pb-10">
        {children}
      </main>
      <PortalNav items={items} variant="bottom" />
    </div>
  );
}
