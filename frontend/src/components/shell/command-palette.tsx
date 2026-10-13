"use client";

import * as Dialog from "@radix-ui/react-dialog";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useRef, useState, useTransition } from "react";
import { CornerDownLeft, KeyRound, LogOut, Search } from "lucide-react";
import { cn } from "@/components/ui/primitives";
import type { NavGroup } from "./nav";
import { NavIcon } from "./nav-icon";

type Command = { id: string; label: string; group: string; keywords: string; run: () => void; icon: React.ReactNode };

/** Matches every word of the query against the label/group/keywords, in any order. */
export function matches(query: string, haystack: string): boolean {
  const h = haystack.toLowerCase();
  return query
    .toLowerCase()
    .split(/\s+/)
    .filter(Boolean)
    .every((word) => h.includes(word));
}

/** ⌘K / Ctrl+K jump-to for staff: keyboard flow beats decoration in the console. */
export function CommandPalette({ groups }: { groups: NavGroup[] }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [cursor, setCursor] = useState(0);
  const [, startTransition] = useTransition();
  const listRef = useRef<HTMLUListElement>(null);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setOpen((o) => !o);
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  const commands = useMemo<Command[]>(() => {
    const go = (href: string) => () => startTransition(() => router.push(href));
    const pages = groups.flatMap((g) =>
      g.items
        .filter((i) => i.ready)
        .map((i) => ({ id: i.href, label: i.label, group: "Go to", keywords: g.label, run: go(i.href), icon: <NavIcon name={i.icon} /> })),
    );
    return [
      ...pages,
      { id: "change-password", label: "Change password", group: "Account", keywords: "security", run: go("/change-password"), icon: <KeyRound className="size-4" aria-hidden /> },
      {
        id: "sign-out",
        label: "Sign out",
        group: "Account",
        keywords: "log out logout",
        run: () => document.querySelector<HTMLFormElement>("form[data-sign-out]")?.requestSubmit(),
        icon: <LogOut className="size-4" aria-hidden />,
      },
    ];
  }, [groups, router]);

  const results = commands.filter((c) => matches(query, `${c.label} ${c.group} ${c.keywords}`));
  const active = Math.min(cursor, Math.max(0, results.length - 1));

  const select = (c: Command | undefined) => {
    if (!c) return;
    setOpen(false);
    c.run();
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      const delta = e.key === "ArrowDown" ? 1 : -1;
      const next = (active + delta + results.length) % Math.max(1, results.length);
      setCursor(next);
      listRef.current?.querySelector(`[data-index="${next}"]`)?.scrollIntoView({ block: "nearest" });
    } else if (e.key === "Enter") {
      e.preventDefault();
      select(results[active]);
    }
  };

  let lastGroup = "";

  return (
    <Dialog.Root
      open={open}
      onOpenChange={(o) => {
        setOpen(o);
        if (!o) {
          setQuery("");
          setCursor(0);
        }
      }}
    >
      <Dialog.Trigger className="hidden min-h-10 w-full max-w-xs items-center gap-2 rounded-input border border-border bg-bg px-3 text-sm text-muted transition-colors hover:border-brand/40 hover:text-text sm:flex">
        <Search className="size-4" aria-hidden />
        <span className="flex-1 text-left">Jump to…</span>
        <kbd className="rounded border border-border bg-surface px-1.5 font-sans text-[0.6875rem]">Ctrl K</kbd>
      </Dialog.Trigger>
      <Dialog.Trigger className="inline-flex size-11 items-center justify-center rounded-input text-muted hover:bg-surface-2 hover:text-text sm:hidden" aria-label="Jump to a page">
        <Search className="size-5" aria-hidden />
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="animate-fade fixed inset-0 z-50 bg-black/40 backdrop-blur-[2px]" />
        <Dialog.Content
          className="animate-rise fixed top-[12vh] left-1/2 z-50 w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 overflow-hidden rounded-card border border-border bg-surface text-text shadow-2xl"
          onKeyDown={onKeyDown}
          aria-describedby={undefined}
        >
          <Dialog.Title className="sr-only">Jump to</Dialog.Title>
          <div className="flex items-center gap-2 border-b border-border px-4">
            <Search className="size-4 text-muted" aria-hidden />
            <input
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setCursor(0);
              }}
              placeholder="Search pages and actions…"
              className="h-12 flex-1 bg-transparent text-base outline-none placeholder:text-muted sm:text-sm"
              role="combobox"
              aria-expanded="true"
              aria-controls="command-list"
              aria-activedescendant={results[active] ? `cmd-${results[active].id}` : undefined}
              aria-autocomplete="list"
            />
            <kbd className="rounded border border-border px-1.5 text-[0.6875rem] text-muted">Esc</kbd>
          </div>
          <ul ref={listRef} id="command-list" role="listbox" aria-label="Results" className="max-h-80 overflow-y-auto p-1.5">
            {results.length === 0 && <li className="px-3 py-8 text-center text-sm text-muted">No matches for &ldquo;{query}&rdquo;</li>}
            {results.map((c, i) => {
              const header = c.group !== lastGroup ? c.group : null;
              lastGroup = c.group;
              return (
                <li key={c.id} role="presentation">
                  {header && <p className="px-3 pt-2 pb-1 text-xs font-medium text-muted">{header}</p>}
                  <div
                    id={`cmd-${c.id}`}
                    role="option"
                    aria-selected={i === active}
                    data-index={i}
                    tabIndex={-1}
                    onMouseMove={() => setCursor(i)}
                    onClick={() => select(c)}
                    onKeyDown={(e) => e.key === "Enter" && select(c)}
                    className={cn("flex min-h-10 cursor-pointer items-center gap-3 rounded-input px-3 text-sm", i === active && "bg-brand/10 text-brand")}
                  >
                    {c.icon}
                    <span className="flex-1">{c.label}</span>
                    {i === active && <CornerDownLeft className="size-3.5 text-muted" aria-hidden />}
                  </div>
                </li>
              );
            })}
          </ul>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
