import Link from "next/link";
import { NotebookPen } from "lucide-react";
import { formatLagos } from "@/lib/time/lagos";
import type { BookSummary } from "@/lib/book/book";
import { Badge, Card } from "@/components/ui/primitives";
import { EmptyState } from "@/components/ui/states";

/** Books with their unread counts and the last note: unread first, then latest activity. */
export function BookList({ books, hrefFor, empty }: { books: BookSummary[]; hrefFor: (b: BookSummary) => string; empty: string }) {
  if (books.length === 0) {
    return (
      <Card>
        <EmptyState icon={NotebookPen} title={empty} />
      </Card>
    );
  }
  const sorted = [...books].sort((a, b) => b.unread - a.unread || (b.lastEntryAt ?? "").localeCompare(a.lastEntryAt ?? "") || a.name.localeCompare(b.name));
  return (
    <Card className="overflow-hidden">
      <ul className="divide-y divide-border">
        {sorted.map((b) => (
          <li key={b.studentId}>
            <Link href={hrefFor(b)} className="flex items-center gap-3 px-5 py-3 hover:bg-surface-2">
              <span className="min-w-0 flex-1">
                <span className="flex items-center gap-2">
                  <span className={b.unread ? "font-semibold" : "font-medium"}>{b.name}</span>
                  <span className="font-mono text-xs text-muted">{b.admissionNumber}</span>
                </span>
                <span className="block truncate text-sm text-muted">{b.lastEntry ?? "No notes yet"}</span>
              </span>
              <span className="shrink-0 text-right">
                {b.unread > 0 && <Badge tone="brand">{b.unread} new</Badge>}
                {b.lastEntryAt && <span className="mt-1 block text-xs text-muted">{formatLagos(b.lastEntryAt)}</span>}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </Card>
  );
}
