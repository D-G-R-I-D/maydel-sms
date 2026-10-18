import Link from "next/link";
import { cn } from "@/components/ui/primitives";

/** Applications / Form codes, keeping the chosen session. */
export function AdmissionsTabs({ active, sessionId }: { active: "applications" | "codes"; sessionId: string }) {
  const q = `?sessionId=${sessionId}`;
  return (
    <nav aria-label="Admissions" className="mb-5 inline-flex rounded-input bg-surface-2 p-1">
      {(
        [
          ["applications", "Applications", `/admin/admissions${q}`],
          ["codes", "Form codes", `/admin/admissions/form-codes${q}`],
        ] as const
      ).map(([key, label, href]) => (
        <Link
          key={key}
          href={href}
          aria-current={active === key ? "page" : undefined}
          className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", active === key ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
        >
          {label}
        </Link>
      ))}
    </nav>
  );
}
