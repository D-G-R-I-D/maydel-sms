"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useTransition } from "react";
import { CalendarDays, Loader2 } from "lucide-react";
import { Select } from "@/components/ui/primitives";

export type TermOption = { id: string; label: string; session: string; isCurrent: boolean };

/** Term selector kept in `?termId=` (defaults to the current term on the server). */
export function TermPicker({ terms, value }: { terms: TermOption[]; value: string }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, startTransition] = useTransition();
  const sessions = [...new Set(terms.map((t) => t.session))];

  return (
    <label className="flex items-center gap-2 text-sm">
      {pending ? <Loader2 className="size-4 animate-spin text-muted" aria-hidden /> : <CalendarDays className="size-4 text-muted" aria-hidden />}
      <span className="sr-only">Term</span>
      <Select
        value={value}
        className="w-auto min-w-56"
        onChange={(e) => {
          const next = new URLSearchParams(params);
          next.set("termId", e.target.value);
          startTransition(() => router.replace(`${pathname}?${next.toString()}`, { scroll: false }));
        }}
      >
        {sessions.map((s) => (
          <optgroup key={s} label={s}>
            {terms
              .filter((t) => t.session === s)
              .map((t) => (
                <option key={t.id} value={t.id}>
                  {t.label}
                  {t.isCurrent ? " (current)" : ""}
                </option>
              ))}
          </optgroup>
        ))}
      </Select>
    </label>
  );
}
