import type { Metadata } from "next";
import Link from "next/link";
import { ChevronRight, UsersRound } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { guardiansHref } from "@/lib/validation/students";
import { Avatar, Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { GuardianSearch } from "@/components/students/guardian-search";

export const metadata: Metadata = { title: "Guardians" };

export default async function GuardiansPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const header = <PageHeader title="Guardians" description="Parents and guardians. Siblings share one guardian record." />;
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

  const params = await searchParams;
  const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v)?.trim() || undefined;
  const search = one(params.search)?.slice(0, 100);
  const pageNum = Number.parseInt(one(params.page) ?? "1", 10);
  const page = Number.isFinite(pageNum) && pageNum > 0 ? pageNum : 1;

  const guardians = await load((api) => api.GET("/api/guardians", { params: { query: { search, page, pageSize: 25 } } }));

  let body: React.ReactNode;
  if (!guardians.ok) body = <ProblemState problem={guardians.problem} />;
  else if (guardians.data.items.length === 0)
    body = search ? (
      <EmptyState
        title="No guardians match"
        action={
          <Link href="/admin/guardians" className={buttonClasses("secondary")}>
            Clear search
          </Link>
        }
      >
        Try a name, an email, or a phone number in any format.
      </EmptyState>
    ) : (
      <EmptyState icon={UsersRound} title="No guardians yet">
        Guardians are added from a student&apos;s profile (Students → a student → Add guardian).
      </EmptyState>
    );
  else
    body = (
      <>
        <ul className="divide-y divide-border">
          {guardians.data.items.map((g) => (
            <li key={g.id}>
              <Link href={`/admin/guardians/${g.id}`} className="flex items-center gap-3 px-4 py-3 transition-colors hover:bg-surface-2/50">
                <Avatar name={g.fullName} className="size-9 text-xs" />
                <span className="min-w-0 flex-1">
                  <span className="block font-medium">{g.fullName}</span>
                  <span className="block truncate text-sm text-muted">
                    {g.phone}
                    {g.email ? ` · ${g.email}` : ""}
                  </span>
                </span>
                <span className="hidden flex-wrap justify-end gap-1.5 sm:flex">
                  <Badge>
                    {g.childCount} {g.childCount === 1 ? "child" : "children"}
                  </Badge>
                  {g.hasLogin ? <Badge tone="success">Has login</Badge> : <Badge tone="warning">No login</Badge>}
                </span>
                <ChevronRight className="size-4 text-muted" aria-hidden />
              </Link>
            </li>
          ))}
        </ul>
        <Pagination page={guardians.data.page} pageSize={guardians.data.pageSize} totalCount={guardians.data.totalCount} hrefFor={(p) => guardiansHref(search, p)} noun="guardians" />
      </>
    );

  return (
    <>
      {header}
      <GuardianSearch search={search}>{body}</GuardianSearch>
    </>
  );
}
