"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useTransition } from "react";
import { ChevronLeft, ChevronRight, School } from "lucide-react";
import { shiftSchoolDay } from "@/lib/attendance/rules";
import { SchoolLoader } from "@/components/ui/school-loader";
import { Button, Input, Select } from "@/components/ui/primitives";

/** Class (when there's a choice) and day for the register, kept in `?classRoomId=&date=`. */
export function RegisterControls({
  classes,
  classRoomId,
  date,
  today,
}: {
  classes: { id: string; name: string; isForm: boolean }[];
  classRoomId: string;
  date: string;
  today: string;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, startTransition] = useTransition();

  const go = (patch: Record<string, string>) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(patch)) next.set(k, v);
    startTransition(() => router.replace(`${pathname}?${next.toString()}`, { scroll: false }));
  };
  const next = shiftSchoolDay(date, 1);

  return (
    <div className="flex flex-wrap items-center gap-2">
      {pending && <SchoolLoader size="xs" />}
      {classes.length > 1 && (
        <label className="flex items-center gap-2 text-sm">
          <School className="size-4 text-muted" aria-hidden />
          <span className="sr-only">Class</span>
          <Select value={classRoomId} className="w-auto min-w-40" onChange={(e) => go({ classRoomId: e.target.value })}>
            {classes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
                {c.isForm ? " (form class)" : ""}
              </option>
            ))}
          </Select>
        </label>
      )}
      <div className="flex items-center gap-1">
        <Button variant="secondary" size="sm" className="min-h-11 px-2.5" onClick={() => go({ date: shiftSchoolDay(date, -1) })} aria-label="Previous school day">
          <ChevronLeft className="size-4" aria-hidden />
        </Button>
        <Input
          type="date"
          aria-label="Date"
          value={date}
          max={today}
          className="w-auto"
          onChange={(e) => e.target.value && go({ date: e.target.value })}
        />
        <Button variant="secondary" size="sm" className="min-h-11 px-2.5" onClick={() => go({ date: next > today ? today : next })} disabled={date >= today} aria-label="Next school day">
          <ChevronRight className="size-4" aria-hidden />
        </Button>
        {date !== today && (
          <Button variant="ghost" size="sm" className="min-h-11" onClick={() => go({ date: today })}>
            Today
          </Button>
        )}
      </div>
    </div>
  );
}
