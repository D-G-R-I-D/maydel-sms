"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition, type ReactNode } from "react";
import { Search } from "lucide-react";
import type { ClassRoomDto, StudentStatus } from "@/lib/api/types";
import { groupBySection } from "@/lib/validation/academics";
import { STUDENT_STATUSES, studentsHref, type StudentSearch } from "@/lib/validation/students";
import { Card, Input, Select, cn } from "@/components/ui/primitives";

/** Debounced search + class + status, all in the URL. Results dim with a progress bar while loading. */
export function StudentFilters({ search, classrooms, children }: { search: StudentSearch; classrooms: ClassRoomDto[]; children: ReactNode }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [text, setText] = useState(search.search ?? "");
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);

  const go = (overrides: Partial<StudentSearch>) => startTransition(() => router.replace(studentsHref(search, { ...overrides, page: 1 }), { scroll: false }));

  return (
    <>
      <Card className="grid gap-3 p-3 sm:grid-cols-[1fr_auto] lg:grid-cols-[1fr_14rem_12rem]">
        <form
          role="search"
          className="relative sm:col-span-2 lg:col-span-1"
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
            placeholder="Search name or admission number"
            aria-label="Search students"
            className="pl-9"
          />
        </form>
        <Select aria-label="Class" value={search.classRoomId ?? ""} onChange={(e) => go({ classRoomId: e.target.value || undefined })}>
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
        <Select aria-label="Status" value={search.status} onChange={(e) => go({ status: e.target.value as StudentStatus | "all" })}>
          {STUDENT_STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
          <option value="all">All statuses</option>
        </Select>
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
