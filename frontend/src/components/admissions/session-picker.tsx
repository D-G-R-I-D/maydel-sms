"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useTransition } from "react";
import { CalendarDays } from "lucide-react";
import { SchoolLoader } from "@/components/ui/school-loader";
import { Select } from "@/components/ui/primitives";

/** Academic session selector kept in `?sessionId=` (admissions are per session). */
export function SessionPicker({ sessions, value }: { sessions: { id: string; name: string; isCurrent: boolean }[]; value: string }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, startTransition] = useTransition();
  return (
    <label className="flex items-center gap-2 text-sm">
      {pending ? <SchoolLoader size="xs" /> : <CalendarDays className="size-4 text-muted" aria-hidden />}
      <span className="sr-only">Session</span>
      <Select
        value={value}
        className="w-auto min-w-44"
        onChange={(e) => {
          const next = new URLSearchParams(params);
          next.set("sessionId", e.target.value);
          next.delete("page");
          startTransition(() => router.replace(`${pathname}?${next.toString()}`, { scroll: false }));
        }}
      >
        {sessions.map((s) => (
          <option key={s.id} value={s.id}>
            {s.name}
            {s.isCurrent ? " (current)" : ""}
          </option>
        ))}
      </Select>
    </label>
  );
}
