"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition, type ReactNode } from "react";
import { Search } from "lucide-react";
import { guardiansHref } from "@/lib/validation/students";
import { Card, Input, cn } from "@/components/ui/primitives";

/** Debounced guardian search (name, email or phone in any format) kept in the URL. */
export function GuardianSearch({ search, children }: { search?: string; children: ReactNode }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [text, setText] = useState(search ?? "");
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);
  const go = (v: string) => startTransition(() => router.replace(guardiansHref(v.trim() || undefined), { scroll: false }));

  return (
    <>
      <Card className="p-3">
        <form
          role="search"
          className="relative"
          onSubmit={(e) => {
            e.preventDefault();
            clearTimeout(timer.current);
            go(text);
          }}
        >
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
          <Input
            value={text}
            onChange={(e) => {
              const v = e.target.value;
              setText(v);
              clearTimeout(timer.current);
              timer.current = setTimeout(() => go(v), 400);
            }}
            placeholder="Search name, email or phone (any format)"
            aria-label="Search guardians"
            className="pl-9"
          />
        </form>
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
