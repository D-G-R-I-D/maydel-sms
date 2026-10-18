import Link from "next/link";
import { cn } from "@/components/ui/primitives";

export type AdmissionsTab = "applications" | "exams" | "results" | "codes";

const all: { key: AdmissionsTab; label: string; path: string; adminOnly?: boolean; staffOnly?: boolean }[] = [
  { key: "applications", label: "Applications", path: "/admin/admissions", staffOnly: true },
  { key: "exams", label: "Exam sittings", path: "/admin/admissions/exams" },
  { key: "results", label: "Results", path: "/admin/admissions/results", adminOnly: true },
  { key: "codes", label: "Form codes", path: "/admin/admissions/form-codes", staffOnly: true },
];

/**
 * Admissions sections, keeping the chosen session. Teachers only see Exam sittings (to enter scores);
 * Results is admin-only; Applications and Form codes are for admin and bursar.
 */
export function AdmissionsTabs({ active, sessionId, isAdmin = true, isBursar = false }: { active: AdmissionsTab; sessionId: string; isAdmin?: boolean; isBursar?: boolean }) {
  const tabs = all.filter((t) => (t.adminOnly ? isAdmin : t.staffOnly ? isAdmin || isBursar : true));
  if (tabs.length < 2) return null;
  const q = `?sessionId=${sessionId}`;
  return (
    <nav aria-label="Admissions" className="mb-5 flex flex-wrap gap-1 rounded-input bg-surface-2 p-1 sm:inline-flex">
      {tabs.map((t) => (
        <Link
          key={t.key}
          href={`${t.path}${q}`}
          aria-current={active === t.key ? "page" : undefined}
          className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", active === t.key ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
        >
          {t.label}
        </Link>
      ))}
    </nav>
  );
}
