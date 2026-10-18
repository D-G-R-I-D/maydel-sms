"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState, useTransition } from "react";
import { Search } from "lucide-react";
import { DIFFICULTIES, KINDS, kindLabel } from "@/lib/cbt/questions";
import { SchoolLoader } from "@/components/ui/school-loader";
import { Input, Select } from "@/components/ui/primitives";

/** Search (debounced), kind, topic and difficulty for a question bank, all in the URL. */
export function QuestionFilters({ topics }: { topics: string[] }) {
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
    <div className="mb-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-[1fr_11rem_12rem_9rem_auto]">
      <form
        role="search"
        className="relative sm:col-span-2 lg:col-span-1"
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
          placeholder="Search the question text"
          aria-label="Search questions"
          className="pl-9"
        />
      </form>
      <Select aria-label="Kind" value={params.get("kind") ?? ""} onChange={(e) => go("kind", e.target.value || undefined)}>
        <option value="">All kinds</option>
        {KINDS.map((k) => (
          <option key={k} value={k}>
            {kindLabel[k]}
          </option>
        ))}
      </Select>
      <Select aria-label="Topic" value={params.get("topic") ?? ""} onChange={(e) => go("topic", e.target.value || undefined)} disabled={topics.length === 0}>
        <option value="">All topics</option>
        {topics.map((t) => (
          <option key={t} value={t}>
            {t}
          </option>
        ))}
      </Select>
      <Select aria-label="Difficulty" value={params.get("difficulty") ?? ""} onChange={(e) => go("difficulty", e.target.value || undefined)}>
        <option value="">Any difficulty</option>
        {DIFFICULTIES.map((d) => (
          <option key={d} value={d}>
            {d}
          </option>
        ))}
      </Select>
      <span className="hidden items-center lg:flex">{pending && <SchoolLoader size="xs" />}</span>
    </div>
  );
}
