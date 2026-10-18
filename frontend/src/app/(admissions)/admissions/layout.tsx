import type { Metadata } from "next";
import Link from "next/link";
import { LogIn } from "lucide-react";
import { getSessionUser } from "@/lib/auth/session";
import { isPortalUser } from "@/lib/auth/roles";
import { SchoolLogo } from "@/components/school/school-logo";
import { UserMenu } from "@/components/shell/user-menu";
import { buttonClasses } from "@/components/ui/primitives";

export const metadata: Metadata = { title: { default: "Admissions", template: "%s · Maydel admissions" } };

/**
 * E11: the families' admissions area. `/admissions/apply` is public (buy a form); the rest needs the
 * family's applicant account (proxy.ts). Parents applying for a sibling get a link back to the portal.
 */
export default async function AdmissionsLayout({ children }: { children: React.ReactNode }) {
  const user = await getSessionUser();
  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-20 border-b border-border bg-surface/90 backdrop-blur">
        <div className="mx-auto flex h-16 w-full max-w-4xl items-center justify-between gap-4 px-4 sm:px-6">
          <Link href={user ? "/admissions" : "/admissions/apply"} className="flex items-center gap-2.5">
            <SchoolLogo width={28} height={35} />
            <span className="font-display text-base font-semibold text-brand">
              Maydel
              <span className="block text-xs font-normal text-muted">Admissions</span>
            </span>
          </Link>
          <div className="flex items-center gap-2">
            {user && isPortalUser(user.roles) && (
              <Link href="/portal" className={buttonClasses("ghost", "sm")}>
                Parent portal
              </Link>
            )}
            {user ? (
              <UserMenu fullName={user.fullName} userName={user.userName} roles={user.roles} />
            ) : (
              <Link href="/login?next=%2Fadmissions" className={buttonClasses("secondary", "sm")}>
                <LogIn className="size-4" aria-hidden />
                Sign in
              </Link>
            )}
          </div>
        </div>
      </header>
      <main id="main" className="mx-auto w-full max-w-4xl flex-1 px-4 py-6 sm:px-6">
        {children}
      </main>
    </div>
  );
}
