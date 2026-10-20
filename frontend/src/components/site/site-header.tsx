"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { LogIn, Menu, X } from "lucide-react";
import { siteNav } from "@/lib/site/content";
import { SchoolLogo } from "@/components/school/school-logo";
import { ThemeToggle } from "@/components/shell/theme-toggle";
import { buttonClasses, cn } from "@/components/ui/primitives";

const isActive = (pathname: string, href: string) => (href === "/" ? pathname === "/" : pathname === href || pathname.startsWith(`${href}/`));

/** E7: the website header. Links across the top from xl up, a menu sheet below; "Portal" signs families and staff in. */
export function SiteHeader({ schoolName }: { schoolName: string }) {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const [lastPath, setLastPath] = useState(pathname);
  // Close the menu after navigating (state reset during render, not in an effect).
  if (lastPath !== pathname) {
    setLastPath(pathname);
    setOpen(false);
  }
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open]);

  return (
    <header className="sticky top-0 z-30 border-b border-border bg-surface/90 backdrop-blur">
      <div className="mx-auto flex h-16 w-full max-w-6xl items-center gap-4 px-4 sm:px-6">
        <Link href="/" className="flex min-w-0 items-center gap-2.5">
          <SchoolLogo width={30} height={38} priority />
          <span className="min-w-0 leading-tight">
            <span className="block truncate font-display text-base font-semibold text-brand">{schoolName}</span>
            <span className="hidden text-xs text-muted sm:block">The Well Spring of Knowledge</span>
          </span>
        </Link>
        <nav aria-label="Main" className="ml-auto hidden xl:block">
          <ul className="flex items-center gap-1">
            {siteNav.map((n) => (
              <li key={n.href}>
                <Link
                  href={n.href}
                  aria-current={isActive(pathname, n.href) ? "page" : undefined}
                  className={cn("inline-flex min-h-10 items-center rounded-input px-2.5 text-sm whitespace-nowrap", isActive(pathname, n.href) ? "bg-brand/10 font-medium text-brand" : "text-text hover:bg-surface-2")}
                >
                  {n.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
        <ThemeToggle className="ml-auto xl:ml-1" />
        <Link href="/login" className={buttonClasses("primary", "sm")}>
          <LogIn className="size-4" aria-hidden />
          Portal
        </Link>
        <button
          type="button"
          className="inline-flex size-11 items-center justify-center rounded-input hover:bg-surface-2 xl:hidden"
          aria-expanded={open}
          aria-controls="site-menu"
          aria-label={open ? "Close menu" : "Open menu"}
          onClick={() => setOpen((v) => !v)}
        >
          {open ? <X className="size-5" aria-hidden /> : <Menu className="size-5" aria-hidden />}
        </button>
      </div>
      {open && (
        <nav id="site-menu" aria-label="Main" className="border-t border-border bg-surface xl:hidden">
          <ul className="mx-auto grid max-w-6xl gap-1 px-4 py-3 sm:px-6">
            {siteNav.map((n) => (
              <li key={n.href}>
                <Link
                  href={n.href}
                  aria-current={isActive(pathname, n.href) ? "page" : undefined}
                  className={cn("flex min-h-11 items-center rounded-input px-3", isActive(pathname, n.href) ? "bg-brand/10 font-medium text-brand" : "hover:bg-surface-2")}
                >
                  {n.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      )}
    </header>
  );
}
