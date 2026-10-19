import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import type { TestDetail } from "@/lib/cbt/tests";
import { testsHref } from "@/lib/cbt/tests";
import { Badge, PageHeader, cn } from "@/components/ui/primitives";

export type TestTab = "build" | "results" | "analytics" | "gradebook";

/**
 * The header shared by a test's pages: back link, title, status, and tabs. Results and analytics
 * appear once it's published; the grade sheet only for tests that count towards a component.
 */
export function TestHeader({ test, active, actions }: { test: TestDetail; active: TestTab; actions?: React.ReactNode }) {
  const tabs: { key: TestTab; label: string; href: string }[] = [
    { key: "build", label: test.status === "Draft" ? "Build" : "Paper", href: `/admin/tests/${test.id}` },
    ...(test.status === "Published"
      ? [
          { key: "results" as const, label: "Results and marking", href: `/admin/tests/${test.id}/results` },
          { key: "analytics" as const, label: "Analytics", href: `/admin/tests/${test.id}/analytics` },
        ]
      : []),
    ...(test.status === "Published" && test.componentId ? [{ key: "gradebook" as const, label: "Grade sheet", href: `/admin/tests/${test.id}/gradebook` }] : []),
  ];
  return (
    <>
      <Link href={testsHref(test.termId)} className="mb-2 inline-flex min-h-11 items-center gap-1.5 text-sm text-brand hover:underline">
        <ArrowLeft className="size-4" aria-hidden />
        Tests &amp; exams
      </Link>
      <PageHeader
        title={test.title}
        description={
          <>
            {test.subject} · {test.classLevel} · {test.term}
          </>
        }
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone={test.status === "Published" ? "success" : "warning"}>{test.status}</Badge>
            {test.resultsReleasedAt && <Badge tone="brand">Results released</Badge>}
            {actions}
          </div>
        }
      />
      {tabs.length > 1 && (
        <nav aria-label="Test" className="mb-5 flex flex-wrap gap-1 rounded-input bg-surface-2 p-1 sm:inline-flex">
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
      )}
    </>
  );
}
