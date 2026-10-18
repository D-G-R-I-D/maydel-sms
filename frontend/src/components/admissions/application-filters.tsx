"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState, useTransition } from "react";
import { Search } from "lucide-react";
import type { ApplicationStatus } from "@/lib/api/types";
import { applicationStatusLabel } from "@/lib/admissions/rules";
import { SchoolLoader } from "@/components/ui/school-loader";
import { Input, Select } from "@/components/ui/primitives";

const STATUSES = Object.keys(applicationStatusLabel) as ApplicationStatus[];

/** Search (debounced), status and class for the applications list, all in the URL. */
export function ApplicationFilters({ levels }: { levels: { id: string; name: string }[] }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, startTransition] = useTransition();
  const [text, setText] = useState(params.get("search") ?? "");
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);

  const go = (key: string, value: string | undefined) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    next.delete("page");
    startTransition(() => router.replace(`${pathname}?${next.toString()}`, { scroll: false }));
  };

  return (
    <div className="mb-4 grid gap-3 sm:grid-cols-[1fr_13rem_12rem_auto]">
      <form
        role="search"
        className="relative"
        onSubmit={(e) => {
          e.preventDefault();
          clearTimeout(timer.current);
          go("search", text.trim() || undefined);
        }}
      >
        <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
        <Input
          value={text}
          onChange={(e) => {
            const v = e.target.value;
            setText(v);
            clearTimeout(timer.current);
            timer.current = setTimeout(() => go("search", v.trim() || undefined), 400);
          }}
          placeholder="Search child, parent, email or reference"
          aria-label="Search applications"
          className="pl-9"
        />
      </form>
      <Select aria-label="Status" value={params.get("status") ?? ""} onChange={(e) => go("status", e.target.value || undefined)}>
        <option value="">All statuses</option>
        {STATUSES.map((s) => (
          <option key={s} value={s}>
            {applicationStatusLabel[s]}
          </option>
        ))}
      </Select>
      <Select aria-label="Class" value={params.get("classLevelId") ?? ""} onChange={(e) => go("classLevelId", e.target.value || undefined)}>
        <option value="">All classes</option>
        {levels.map((l) => (
          <option key={l.id} value={l.id}>
            {l.name}
          </option>
        ))}
      </Select>
      <span className="hidden items-center sm:flex">{pending && <SchoolLoader size="xs" />}</span>
    </div>
  );
}
