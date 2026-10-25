import Link from "next/link";
import { cn } from "@/components/ui/primitives";

const tabs = [
  { key: "today", label: "Today", href: "/admin/transport" },
  { key: "reports", label: "Reports", href: "/admin/transport/reports" },
  { key: "checks", label: "Checklists", href: "/admin/transport/checks" },
  { key: "setup", label: "Setup", href: "/admin/transport/setup" },
] as const;

/** E18 admin: Transport sections. */
export function TransportTabs({ active }: { active: (typeof tabs)[number]["key"] }) {
  return (
    <nav aria-label="Transport" className="mb-5 inline-flex flex-wrap rounded-input bg-surface-2 p-1">
      {tabs.map((t) => (
        <Link
          key={t.key}
          href={t.href}
          aria-current={active === t.key ? "page" : undefined}
          className={cn("inline-flex min-h-9 items-center rounded-[6px] px-3 text-sm transition-colors", active === t.key ? "bg-surface font-medium text-text shadow-low" : "text-muted hover:text-text")}
        >
          {t.label}
        </Link>
      ))}
    </nav>
  );
}
