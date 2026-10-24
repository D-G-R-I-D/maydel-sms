"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/components/ui/primitives";
import { activeHref, type NavItem } from "./nav";
import { NavIcon } from "./nav-icon";

/** Bottom tab bar on phones and tablets, inline tabs in the header from lg up (six tabs need the room). Unreleased tabs are disabled. */
export function PortalNav({ items, variant }: { items: NavItem[]; variant: "bottom" | "top" }) {
  const pathname = usePathname();
  const active = activeHref(pathname, items.filter((i) => i.ready).map((i) => i.href));

  if (variant === "top") {
    return (
      <nav aria-label="Portal" className="hidden min-w-0 lg:block">
        <ul className="flex items-center gap-1">
          {items.map((item) => (
            <li key={item.href}>
              {item.ready ? (
                <Link
                  href={item.href}
                  aria-current={active === item.href ? "page" : undefined}
                  className={cn(
                    "inline-flex min-h-10 items-center gap-2 rounded-input px-3 text-sm",
                    active === item.href ? "bg-brand/10 font-medium text-brand" : "text-text hover:bg-surface-2",
                  )}
                >
                  <NavIcon name={item.icon} />
                  {item.label}
                </Link>
              ) : (
                <span className="inline-flex min-h-10 cursor-not-allowed items-center gap-2 px-3 text-sm text-muted" aria-disabled="true" title="Coming soon">
                  <NavIcon name={item.icon} />
                  {item.label}
                </span>
              )}
            </li>
          ))}
        </ul>
      </nav>
    );
  }

  return (
    <nav aria-label="Portal" className="fixed inset-x-0 bottom-0 z-30 border-t border-border bg-surface pb-[env(safe-area-inset-bottom)] lg:hidden">
      <ul className={cn("grid", items.length > 5 ? "grid-cols-6" : "grid-cols-5")}>
        {items.map((item) => (
          <li key={item.href}>
            {item.ready ? (
              <Link
                href={item.href}
                aria-current={active === item.href ? "page" : undefined}
                className={cn("flex min-h-14 flex-col items-center justify-center gap-0.5 text-[0.6875rem]", active === item.href ? "font-medium text-brand" : "text-muted")}
              >
                <NavIcon name={item.icon} className="size-5" />
                {item.label}
              </Link>
            ) : (
              <span className="flex min-h-14 cursor-not-allowed flex-col items-center justify-center gap-0.5 text-[0.6875rem] text-muted/60" aria-disabled="true">
                <NavIcon name={item.icon} className="size-5" />
                {item.label}
                <span className="sr-only">(coming soon)</span>
              </span>
            )}
          </li>
        ))}
      </ul>
    </nav>
  );
}
