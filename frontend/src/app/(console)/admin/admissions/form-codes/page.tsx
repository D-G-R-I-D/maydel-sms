import type { Metadata } from "next";
import Link from "next/link";
import { KeyRound } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadAdmissionSessions } from "@/lib/admissions/sessions";
import { formatNaira } from "@/lib/finance/rules";
import { Badge, Card, PageHeader, cn } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { SessionPicker } from "@/components/admissions/session-picker";
import { AdmissionsTabs } from "@/components/admissions/admissions-tabs";
import { IssueFormCodes } from "@/components/admissions/issue-form-codes";

export const metadata: Metadata = { title: "Form codes" };

const PAGE_SIZE = 50;
const timeFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });
const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E11.4: one-time codes for families who paid for the form at the school. */
export default async function FormCodesPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Admissions" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const { sessions, selected } = await loadAdmissionSessions(one(sp.sessionId));
  if (!selected) {
    return (
      <>
        <PageHeader title="Admissions" />
        <Card>
          <EmptyState title="No sessions yet">Set up the school calendar under Academics first.</EmptyState>
        </Card>
      </>
    );
  }
  const usedParam = one(sp.used);
  const used = usedParam === "true" ? true : usedParam === "false" ? false : undefined;
  const page = Math.max(1, Number(one(sp.page)) || 1);
  const codes = await load((api) => api.GET("/api/admissions/form-codes", { params: { query: { sessionId: selected.id, used, page, pageSize: PAGE_SIZE } } }));
  const href = (u: boolean | undefined, p = 1) => {
    const q = new URLSearchParams({ sessionId: selected.id });
    if (u !== undefined) q.set("used", String(u));
    if (p > 1) q.set("page", String(p));
    return `/admin/admissions/form-codes?${q.toString()}`;
  };

  return (
    <>
      <PageHeader
        title="Admissions"
        description="Issue a form code when a family pays for the admission form at the school. They enter it in their account instead of paying online."
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <SessionPicker sessions={sessions} value={selected.id} />
            <IssueFormCodes sessionId={selected.id} session={selected.name} />
          </div>
        }
      />
      <AdmissionsTabs active="codes" sessionId={selected.id} />
      <nav aria-label="Filter codes" className="mb-4 flex flex-wrap gap-1 text-sm">
        {(
          [
            [undefined, "All"],
            [false, "Unused"],
            [true, "Used"],
          ] as const
        ).map(([u, label]) => (
          <Link
            key={label}
            href={href(u)}
            aria-current={used === u ? "page" : undefined}
            className={cn("rounded-full border px-3 py-1", used === u ? "border-brand bg-brand/10 font-medium text-brand" : "border-border text-muted hover:text-text")}
          >
            {label}
          </Link>
        ))}
      </nav>
      <Card className="overflow-hidden">
        {!codes.ok ? (
          <ProblemState problem={codes.problem} />
        ) : codes.data.items.length === 0 ? (
          <EmptyState icon={KeyRound} title="No form codes">
            Use “Issue form codes” after a family pays at the school.
          </EmptyState>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[40rem] text-sm">
                <thead className="text-left text-xs uppercase tracking-wider text-muted">
                  <tr className="border-b border-border">
                    <th className="px-4 py-2.5 font-medium">Code</th>
                    <th className="px-3 py-2.5 text-right font-medium">Paid</th>
                    <th className="px-3 py-2.5 font-medium">Issued to</th>
                    <th className="px-3 py-2.5 font-medium">Issued</th>
                    <th className="px-4 py-2.5 font-medium">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {codes.data.items.map((c) => (
                    <tr key={c.id}>
                      <td className="px-4 py-2.5 font-mono font-semibold tracking-wider">{c.code}</td>
                      <td className="tabular px-3 py-2.5 text-right">{formatNaira(c.amountPaid)}</td>
                      <td className="px-3 py-2.5">{c.issuedTo ?? <span className="text-muted">—</span>}</td>
                      <td className="px-3 py-2.5 whitespace-nowrap text-muted">{timeFormat.format(new Date(c.issuedAt))}</td>
                      <td className="px-4 py-2.5">
                        {c.used ? (
                          <span className="flex flex-col">
                            <Badge>Used</Badge>
                            {c.usedByReference && <span className="tabular mt-1 text-xs text-muted">{c.usedByReference}</span>}
                          </span>
                        ) : (
                          <Badge tone="success">Unused</Badge>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="border-t border-border px-4 py-3">
              <Pagination page={codes.data.page} pageSize={codes.data.pageSize} totalCount={codes.data.totalCount} hrefFor={(p) => href(used, p)} noun="codes" />
            </div>
          </>
        )}
      </Card>
    </>
  );
}
