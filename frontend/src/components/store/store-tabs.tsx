import Link from "next/link";
import { cn } from "@/components/ui/primitives";

const tabs = [
  { key: "catalogue", label: "Catalogue", href: "/admin/store" },
  { key: "orders", label: "Orders", href: "/admin/store/orders" },
  { key: "sell", label: "Sell", href: "/admin/store/sell" },
  { key: "settings", label: "Settings", href: "/admin/store/settings" },
] as const;

/** E19 admin: School store sections. */
export function StoreTabs({ active }: { active: (typeof tabs)[number]["key"] }) {
  return (
    <nav aria-label="Store" className="mb-5 inline-flex flex-wrap rounded-input bg-surface-2 p-1">
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
