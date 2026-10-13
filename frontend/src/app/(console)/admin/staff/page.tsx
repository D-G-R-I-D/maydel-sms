import type { Metadata } from "next";
import Link from "next/link";
import { ChevronRight, KeyRound, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { formatLastSeen, parseStaffSearch, staffHref } from "@/lib/validation/staff";
import { Avatar, Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { AddStaffButton } from "@/components/staff/add-staff";
import { StaffFilters } from "@/components/staff/staff-filters";

export const metadata: Metadata = { title: "Staff" };

const PAGE_SIZE = 25;

function StatusBadge({ active, hasLogin }: { active: boolean; hasLogin: boolean }) {
  if (!active) return <Badge>Inactive</Badge>;
  if (!hasLogin) return <Badge tone="warning">No login</Badge>;
  return <Badge tone="success">Active</Badge>;
}

export default async function StaffPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const header = (actions?: React.ReactNode) => <PageHeader title="Staff" description="Staff records and their logins." actions={actions} />;
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        {header()}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }

  const search = parseStaffSearch(await searchParams);
  const staff = await load((api) =>
    api.GET("/api/staff", {
      params: {
        query: {
          search: search.search,
          isActive: search.status === "all" ? undefined : search.status === "active",
          page: search.page,
          pageSize: PAGE_SIZE,
        },
      },
    }),
  );

  let body: React.ReactNode;
  if (!staff.ok) {
    body = <ProblemState problem={staff.problem} />;
  } else if (staff.data.items.length === 0) {
    body =
      search.search || search.status !== "active" ? (
        <EmptyState
          title="No staff match"
          action={
            <Link href="/admin/staff" className={buttonClasses("secondary")}>
              Clear search
            </Link>
          }
        >
          Try another name, email or staff number.
        </EmptyState>
      ) : (
        <EmptyState icon={Users} title="No staff yet" action={<AddStaffButton />}>
          Add teachers, bursars and administrators. Each gets a login with a one-time password slip.
        </EmptyState>
      );
  } else {
    const rows = staff.data.items;
    body = (
      <>
        <table className="hidden w-full text-left text-sm md:table">
          <caption className="sr-only">Staff</caption>
          <thead className="bg-surface-2 text-xs uppercase tracking-wide text-muted">
            <tr>
              <th scope="col" className="px-4 py-2.5 font-medium">Name</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Staff no.</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Roles</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Status</th>
              <th scope="col" className="px-4 py-2.5 font-medium">Last sign-in</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {rows.map((s) => (
              <tr key={s.id} className="transition-colors hover:bg-surface-2/50">
                <th scope="row" className="px-4 py-3 font-normal">
                  <Link href={`/admin/staff/${s.id}`} className="group flex items-center gap-3">
                    <Avatar name={s.fullName} className="size-8 text-xs" />
                    <span className="min-w-0">
                      <span className="block font-medium group-hover:text-brand group-hover:underline">{s.fullName}</span>
                      <span className="block truncate text-xs text-muted">{s.email}</span>
                    </span>
                  </Link>
                </th>
                <td className="tabular px-4 py-3 font-mono text-xs">{s.staffNumber}</td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-1">
                    {s.roles.map((r) => (
                      <Badge key={r} tone={r === "Admin" ? "brand" : "neutral"}>
                        {r}
                      </Badge>
                    ))}
                  </div>
                </td>
                <td className="px-4 py-3">
                  <StatusBadge active={s.isActive} hasLogin={s.hasLogin} />
                </td>
                <td className="tabular px-4 py-3 text-muted">{formatLastSeen(s.lastLoginAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <ul className="divide-y divide-border md:hidden">
          {rows.map((s) => (
            <li key={s.id}>
              <Link href={`/admin/staff/${s.id}`} className="flex items-center gap-3 px-4 py-3">
                <Avatar name={s.fullName} className="size-9 text-xs" />
                <span className="min-w-0 flex-1">
                  <span className="block font-medium">{s.fullName}</span>
                  <span className="block truncate text-xs text-muted">
                    {s.staffNumber} · {s.roles.join(", ")}
                  </span>
                  <span className="mt-1 flex items-center gap-2 text-xs text-muted">
                    <StatusBadge active={s.isActive} hasLogin={s.hasLogin} />
                    <KeyRound className="size-3" aria-hidden />
                    {formatLastSeen(s.lastLoginAt)}
                  </span>
                </span>
                <ChevronRight className="size-4 text-muted" aria-hidden />
              </Link>
            </li>
          ))}
        </ul>
        <Pagination page={staff.data.page} pageSize={staff.data.pageSize} totalCount={staff.data.totalCount} hrefFor={(page) => staffHref(search, { page })} noun="staff" />
      </>
    );
  }

  return (
    <>
      {header(<AddStaffButton />)}
      <StaffFilters search={search}>{body}</StaffFilters>
    </>
  );
}
