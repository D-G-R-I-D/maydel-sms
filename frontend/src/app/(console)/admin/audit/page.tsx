import type { Metadata } from "next";
import Link from "next/link";
import { ScrollText } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { auditHref, hasFilters, parseAuditSearch, toApiQuery } from "@/lib/audit/query";
import { Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { AuditFilterBar } from "@/components/audit/filter-bar";
import { AuditTable } from "@/components/audit/audit-table";

export const metadata: Metadata = { title: "Audit log" };

export default async function AuditPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const header = <PageHeader title="Audit log" description="Every change to school records: who, what, when and from where. Newest first." />;
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        {header}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  const search = parseAuditSearch(await searchParams);
  const [filters, entries] = await Promise.all([
    load((api) => api.GET("/api/audit/filters")),
    load((api) => api.GET("/api/audit", { params: { query: toApiQuery(search) } })),
  ]);

  let results: React.ReactNode;
  if (!entries.ok) {
    results = (
      <ProblemState
        problem={entries.problem}
        action={
          <Link href={auditHref(search)} className={buttonClasses("secondary")}>
            Try again
          </Link>
        }
      />
    );
  } else if (entries.data.items.length === 0) {
    results = hasFilters(search) ? (
      <EmptyState
        title="No changes match these filters"
        action={
          <Link href="/admin/audit" className={buttonClasses("secondary")}>
            Clear filters
          </Link>
        }
      >
        Try a wider date range or a different record type.
      </EmptyState>
    ) : (
      <EmptyState icon={ScrollText} title="Nothing has been recorded yet">
        Changes to school records appear here automatically as staff use the system.
      </EmptyState>
    );
  } else {
    results = (
      <>
        <AuditTable entries={entries.data.items} />
        <Pagination
          page={entries.data.page}
          pageSize={entries.data.pageSize}
          totalCount={entries.data.totalCount}
          hrefFor={(page) => auditHref(search, { page })}
          noun="changes"
        />
      </>
    );
  }

  return (
    <>
      {header}
      <AuditFilterBar search={search} entityTypes={filters.ok ? filters.data.entityTypes : []} actions={filters.ok ? filters.data.actions : ["Added", "Modified", "Deleted"]}>
        {results}
      </AuditFilterBar>
    </>
  );
}
