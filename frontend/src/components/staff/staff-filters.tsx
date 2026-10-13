"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition, type ReactNode } from "react";
import { Search } from "lucide-react";
import { staffHref, type StaffSearch } from "@/lib/validation/staff";
import { Card, Input, cn } from "@/components/ui/primitives";

/** Search (debounced) + Active/Inactive/All, in the URL. Results dim with a progress bar while loading. */
export function StaffFilters({ search, children }: { search: StaffSearch; children: ReactNode }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [text, setText] = useState(search.search ?? "");
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);

  const go = (overrides: Partial<StaffSearch>) => startTransition(() => router.replace(staffHref(search, { ...overrides, page: 1 }), { scroll: false }));

  return (
    <>
      <Card className="flex flex-col gap-3 p-3 sm:flex-row sm:items-center">
        <form
          role="search"
          className="relative flex-1"
          onSubmit={(e) => {
            e.preventDefault();
            clearTimeout(timer.current);
            go({ search: text.trim() || undefined });
          }}
        >
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
          <Input
            value={text}
            onChange={(e) => {
              const v = e.target.value;
              setText(v);
              clearTimeout(timer.current);
              timer.current = setTimeout(() => go({ search: v.trim() || undefined }), 400);
            }}
            placeholder="Search name, email or staff number"
            aria-label="Search staff"
            className="pl-9"
          />
        </form>
        <div role="radiogroup" aria-label="Status" className="inline-flex rounded-input bg-surface-2 p-1">
          {(["active", "inactive", "all"] as const).map((s) => (
            <button
              key={s}
              type="button"
              role="radio"
              aria-checked={search.status === s}
              onClick={() => go({ status: s })}
              className={cn(
                "min-h-9 rounded-[6px] px-3 text-sm capitalize transition-colors",
                search.status === s ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text",
              )}
            >
              {s}
            </button>
          ))}
        </div>
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
