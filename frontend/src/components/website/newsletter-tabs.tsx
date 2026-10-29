import Link from "next/link";
import { cn } from "@/components/ui/primitives";

/** Website → Newsletter: the newsletters and the subscriber list. */
export function NewsletterTabs({ active }: { active: "newsletters" | "subscribers" }) {
  return (
    <nav aria-label="Newsletter" className="mb-5 inline-flex rounded-input bg-surface-2 p-1">
      {(
        [
          { key: "newsletters", label: "Newsletters", href: "/admin/website/newsletters" },
          { key: "subscribers", label: "Subscribers", href: "/admin/website/subscribers" },
        ] as const
      ).map((t) => (
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
