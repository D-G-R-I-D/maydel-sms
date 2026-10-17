"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { Menu, X } from "lucide-react";
import { cn } from "@/components/ui/primitives";
import { activeHref, type NavGroup } from "./nav";
import { NavIcon } from "./nav-icon";

function NavList({ groups, onNavigate }: { groups: NavGroup[]; onNavigate?: () => void }) {
  const pathname = usePathname();
  const active = activeHref(pathname, groups.flatMap((g) => g.items.filter((i) => i.ready).map((i) => i.href)));
  return (
    <nav aria-label="Console" className="space-y-5">
      {groups.map((group) => (
        <div key={group.label}>
          {!(group.items.length === 1 && group.items[0].label === group.label) && (
            <p className="mb-1 px-3 text-xs font-semibold uppercase tracking-wider text-muted">{group.label}</p>
          )}
          <ul className="space-y-0.5">
            {group.items.map((item) => (
              <li key={item.href}>
                {item.ready ? (
                  <Link
                    href={item.href}
                    onClick={onNavigate}
                    aria-current={active === item.href ? "page" : undefined}
                    className={cn(
                      "flex min-h-10 items-center gap-3 rounded-input px-3 text-sm transition-colors duration-150",
                      active === item.href ? "bg-brand/10 font-medium text-brand" : "text-text hover:bg-surface-2",
                    )}
                  >
                    <NavIcon name={item.icon} />
                    {item.label}
                    {item.badge ? (
                      <span className="tabular ml-auto rounded-full bg-warning px-2 py-0.5 text-[0.6875rem] font-semibold text-white" aria-label={`${item.badge} waiting`}>
                        {item.badge}
                      </span>
                    ) : null}
                  </Link>
                ) : (
                  <span className="flex min-h-10 cursor-not-allowed items-center gap-3 rounded-input px-3 text-sm text-muted" aria-disabled="true">
                    <NavIcon name={item.icon} />
                    {item.label}
                    <span className="ml-auto rounded-full bg-surface-2 px-2 py-0.5 text-[0.6875rem]">Soon</span>
                  </span>
                )}
              </li>
            ))}
          </ul>
        </div>
      ))}
    </nav>
  );
}

function Brand() {
  return (
    <Link href="/admin" className="flex items-center gap-2.5 px-3">
      <Image src="/brand/crest.svg" alt="" width={28} height={35} />
      <span className="font-display text-base font-semibold leading-tight text-brand">
        Maydel
        <span className="block text-xs font-normal text-muted">Staff console</span>
      </span>
    </Link>
  );
}

/** Fixed sidebar from lg up; a drawer behind a menu button below that. */
export function ConsoleSidebar({ groups }: { groups: NavGroup[] }) {
  const [open, setOpen] = useState(false);
  const closeButton = useRef<HTMLButtonElement>(null);
  const pathname = usePathname();
  const [lastPath, setLastPath] = useState(pathname);
  if (pathname !== lastPath) {
    setLastPath(pathname);
    setOpen(false);
  }

  useEffect(() => {
    if (!open) return;
    closeButton.current?.focus();
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open]);

  return (
    <>
      <aside className="hidden w-64 shrink-0 border-r border-border bg-surface lg:block">
        <div className="sticky top-0 flex h-dvh flex-col gap-6 overflow-y-auto px-3 py-5">
          <Brand />
          <NavList groups={groups} />
        </div>
      </aside>

      <button
        type="button"
        onClick={() => setOpen(true)}
        className="fixed top-2.5 left-2 z-30 inline-flex size-11 items-center justify-center rounded-input text-text hover:bg-surface-2 lg:hidden"
        aria-label="Open navigation"
        aria-expanded={open}
        aria-controls="console-drawer"
      >
        <Menu className="size-5" aria-hidden />
      </button>

      {open && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button type="button" className="absolute inset-0 bg-black/40" aria-label="Close navigation" onClick={() => setOpen(false)} />
          <div
            id="console-drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Navigation"
            className="absolute inset-y-0 left-0 flex w-72 max-w-[85vw] flex-col gap-6 overflow-y-auto bg-surface px-3 py-5 shadow-low"
          >
            <div className="flex items-center justify-between">
              <Brand />
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="inline-flex size-11 items-center justify-center rounded-input hover:bg-surface-2"
                aria-label="Close navigation"
                ref={closeButton}
              >
                <X className="size-5" aria-hidden />
              </button>
            </div>
            <NavList groups={groups} onNavigate={() => setOpen(false)} />
          </div>
        </div>
      )}
    </>
  );
}
