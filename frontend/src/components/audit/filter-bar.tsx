"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition, type ReactNode } from "react";
import { ChevronDown, Filter, SlidersHorizontal, X } from "lucide-react";
import { auditHref, hasFilters, type AuditSearch } from "@/lib/audit/query";
import { Card, Input, Select, buttonClasses, cn } from "@/components/ui/primitives";

const chipLabels: Record<"entityType" | "action" | "entityId" | "userId" | "from" | "to", string> = {
  entityType: "Type",
  action: "Action",
  entityId: "ID",
  userId: "User",
  from: "From",
  to: "To",
};

/**
 * Filters live in the URL. It's a real GET form (works before hydration / without JS); once
 * hydrated, selects and dates apply instantly and the record ID applies after a short pause.
 * `children` (the server-rendered results) dim with a progress bar while the next page loads.
 */
export function AuditFilterBar({
  search,
  entityTypes,
  actions,
  children,
}: {
  search: AuditSearch;
  entityTypes: string[];
  actions: string[];
  children: ReactNode;
}) {
  const router = useRouter();
  const form = useRef<HTMLFormElement>(null);
  const debounce = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const [pending, startTransition] = useTransition();
  const [showFilters, setShowFilters] = useState(false);

  useEffect(() => () => clearTimeout(debounce.current), []);

  // The ID box is left mounted while typing (no re-key), so sync it when a chip/clear changes the URL.
  useEffect(() => {
    const input = form.current?.elements.namedItem("entityId");
    if (input instanceof HTMLInputElement && document.activeElement !== input) input.value = search.entityId ?? "";
  }, [search.entityId]);

  const apply = () => {
    if (!form.current) return;
    const data = new FormData(form.current);
    const read = (k: string) => {
      const v = data.get(k);
      return typeof v === "string" && v.trim() ? v.trim() : undefined;
    };
    const href = auditHref({ entityType: read("entityType"), action: read("action"), entityId: read("entityId"), userId: search.userId, from: read("from"), to: read("to"), page: 1 });
    startTransition(() => router.replace(href, { scroll: false }));
  };

  const applySoon = () => {
    clearTimeout(debounce.current);
    debounce.current = setTimeout(apply, 450);
  };

  const chips = (Object.keys(chipLabels) as (keyof typeof chipLabels)[]).filter((k) => search[k]);

  return (
    <>
      <Card className="p-4">
        <button
          type="button"
          onClick={() => setShowFilters((v) => !v)}
          aria-expanded={showFilters}
          aria-controls="audit-filters"
          className="flex min-h-11 w-full items-center gap-2 text-sm font-medium md:hidden"
        >
          <SlidersHorizontal className="size-4 text-brand" aria-hidden />
          Filters
          {chips.length > 0 && <span className="rounded-full bg-brand px-2 py-0.5 text-xs text-on-brand">{chips.length}</span>}
          <ChevronDown className={cn("ml-auto size-4 text-muted transition-transform duration-200", showFilters && "rotate-180")} aria-hidden />
        </button>
        <form
          id="audit-filters"
          key={[search.entityType, search.action, search.from, search.to].join("|")}
          ref={form}
          method="get"
          action="/admin/audit"
          onSubmit={(e) => {
            e.preventDefault();
            clearTimeout(debounce.current);
            apply();
          }}
          className={cn(
            "gap-3 sm:grid-cols-2 md:grid lg:grid-cols-[1fr_1fr_1fr_auto_auto] lg:items-end",
            showFilters ? "animate-fade mt-3 grid md:mt-0" : "hidden",
          )}
        >
          <div className="space-y-1.5">
            <label htmlFor="f-entityType" className="block text-sm font-medium">
              Record type
            </label>
            <Select id="f-entityType" name="entityType" defaultValue={search.entityType ?? ""} onChange={apply}>
              <option value="">All types</option>
              {entityTypes.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </Select>
          </div>
          <div className="space-y-1.5">
            <label htmlFor="f-action" className="block text-sm font-medium">
              Action
            </label>
            <Select id="f-action" name="action" defaultValue={search.action ?? ""} onChange={apply}>
              <option value="">All actions</option>
              {actions.map((a) => (
                <option key={a} value={a}>
                  {a}
                </option>
              ))}
            </Select>
          </div>
          <div className="space-y-1.5">
            <label htmlFor="f-entityId" className="block text-sm font-medium">
              Record ID
            </label>
            <Input id="f-entityId" name="entityId" defaultValue={search.entityId ?? ""} placeholder="Exact ID" autoComplete="off" spellCheck={false} onChange={applySoon} />
          </div>
          <fieldset className="grid grid-cols-2 gap-2 sm:col-span-2 lg:col-span-1">
            <legend className="sr-only">Date range (school days, inclusive)</legend>
            <div className="space-y-1.5">
              <label htmlFor="f-from" className="block text-sm font-medium">
                From
              </label>
              <Input id="f-from" name="from" type="date" defaultValue={search.from ?? ""} max={search.to} onChange={apply} />
            </div>
            <div className="space-y-1.5">
              <label htmlFor="f-to" className="block text-sm font-medium">
                To
              </label>
              <Input id="f-to" name="to" type="date" defaultValue={search.to ?? ""} min={search.from} onChange={apply} />
            </div>
          </fieldset>
          <div className="flex gap-2 sm:col-span-2 lg:col-span-1">
            <button type="submit" className={buttonClasses("primary", "md", "flex-1 lg:flex-none")}>
              <Filter className="size-4" aria-hidden />
              Apply
            </button>
          </div>
        </form>

        {chips.length > 0 && (
          <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-border pt-3">
            <span className="text-xs text-muted">Filtered by</span>
            {chips.map((k) => (
              <Link
                key={k}
                href={auditHref(search, { [k]: undefined, page: 1 })}
                scroll={false}
                className="animate-fade inline-flex min-h-8 items-center gap-1.5 rounded-full bg-brand/10 py-1 pr-2 pl-3 text-xs font-medium text-brand transition-colors hover:bg-brand/20"
                aria-label={`Remove filter ${chipLabels[k]}: ${search[k]}`}
              >
                <span className="text-brand/70">{chipLabels[k]}:</span>
                <span className="max-w-40 truncate">{search[k]}</span>
                <X className="size-3.5" aria-hidden />
              </Link>
            ))}
            {hasFilters(search) && (
              <Link href="/admin/audit" scroll={false} className="ml-1 text-xs font-medium text-muted underline-offset-4 hover:text-text hover:underline">
                Clear all
              </Link>
            )}
          </div>
        )}
      </Card>

      <Card className="relative mt-4 overflow-hidden" aria-busy={pending}>
        <div className={cn("pointer-events-none absolute inset-x-0 top-0 z-10 h-0.5 overflow-hidden", !pending && "hidden")} aria-hidden>
          <div className="animate-progress h-full w-1/3 bg-brand" />
        </div>
        <div className={cn("transition-opacity duration-200", pending && "opacity-50")}>{children}</div>
      </Card>
    </>
  );
}
