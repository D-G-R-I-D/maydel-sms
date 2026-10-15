"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useTransition } from "react";
import type { ClassRoomDto } from "@/lib/api/types";
import { groupBySection } from "@/lib/validation/academics";
import { SchoolLoader } from "@/components/ui/school-loader";
import { Select } from "@/components/ui/primitives";
import { Switch } from "@/components/ui/switch";

/** Class and "Overdue only" for the debtors list, kept in the URL (the term uses TermPicker). */
export function DebtorFilters({ classrooms, classRoomId, overdueOnly }: { classrooms: ClassRoomDto[]; classRoomId: string | undefined; overdueOnly: boolean }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, startTransition] = useTransition();
  const go = (key: string, value: string | undefined) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    startTransition(() => router.replace(`${pathname}?${next.toString()}`, { scroll: false }));
  };

  return (
    <div className="flex flex-wrap items-center gap-3">
      {pending && <SchoolLoader size="xs" />}
      <Select aria-label="Class" value={classRoomId ?? ""} className="w-auto min-w-44" onChange={(e) => go("classRoomId", e.target.value || undefined)}>
        <option value="">All classes</option>
        {groupBySection(classrooms).map((g) => (
          <optgroup key={g.section} label={g.label}>
            {g.rows.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </optgroup>
        ))}
      </Select>
      <span className="flex items-center gap-2 text-sm">
        <Switch checked={overdueOnly} onCheckedChange={(v) => go("overdueOnly", v ? "true" : undefined)} label="Overdue only" />
        <span aria-hidden>Overdue only</span>
      </span>
    </div>
  );
}
