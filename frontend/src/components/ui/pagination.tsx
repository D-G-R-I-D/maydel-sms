import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { pageCount } from "@/lib/audit/query";
import { buttonClasses, cn } from "./primitives";

export function Pagination({
  page,
  pageSize,
  totalCount,
  hrefFor,
  noun = "items",
}: {
  page: number;
  pageSize: number;
  totalCount: number;
  hrefFor: (page: number) => string;
  noun?: string;
}) {
  const pages = pageCount(totalCount, pageSize);
  const start = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const end = Math.min(totalCount, page * pageSize);
  const nav = (target: number, label: string, icon: React.ReactNode, disabled: boolean) =>
    disabled ? (
      <span className={cn(buttonClasses("secondary", "sm"), "pointer-events-none opacity-50")} aria-disabled="true">
        {icon}
        <span className="sr-only sm:not-sr-only">{label}</span>
      </span>
    ) : (
      <Link href={hrefFor(target)} className={buttonClasses("secondary", "sm")} aria-label={`${label} page`}>
        {icon}
        <span className="sr-only sm:not-sr-only">{label}</span>
      </Link>
    );

  return (
    <nav aria-label="Pagination" className="flex items-center justify-between gap-3 border-t border-border px-4 py-3 text-sm">
      <p className="tabular text-muted">
        {start.toLocaleString("en-NG")}–{end.toLocaleString("en-NG")} of {totalCount.toLocaleString("en-NG")} {noun}
      </p>
      <div className="flex items-center gap-2">
        {nav(page - 1, "Previous", <ChevronLeft className="size-4" aria-hidden />, page <= 1)}
        <span className="tabular hidden text-muted sm:inline">
          Page {page} of {pages}
        </span>
        {nav(page + 1, "Next", <ChevronRight className="size-4" aria-hidden />, page >= pages)}
      </div>
    </nav>
  );
}
