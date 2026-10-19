"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useTransition } from "react";
import { SchoolLoader } from "@/components/ui/school-loader";
import { Select } from "@/components/ui/primitives";

type Option = { id: string; name: string };

/** Subject, class level and status for the tests list, kept in the URL alongside `termId`. */
export function TestFilters({ subjects, levels }: { subjects: Option[]; levels: Option[] }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, startTransition] = useTransition();
  const go = (key: string, value: string) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    startTransition(() => router.replace(`${pathname}?${next.toString()}`, { scroll: false }));
  };
  const select = (key: string, label: string, all: string, options: Option[]) => (
    <Select aria-label={label} value={params.get(key) ?? ""} onChange={(e) => go(key, e.target.value)}>
      <option value="">{all}</option>
      {options.map((o) => (
        <option key={o.id} value={o.id}>
          {o.name}
        </option>
      ))}
    </Select>
  );

  return (
    <div className="mb-4 grid gap-3 sm:grid-cols-[1fr_1fr_12rem_auto]">
      {select("subjectId", "Subject", "All subjects", subjects)}
      {select("classLevelId", "Class", "All classes", levels)}
      {select("status", "Status", "Drafts and published", [
        { id: "Draft", name: "Drafts" },
        { id: "Published", name: "Published" },
      ])}
      <span className="hidden items-center sm:flex">{pending && <SchoolLoader size="xs" />}</span>
    </div>
  );
}
