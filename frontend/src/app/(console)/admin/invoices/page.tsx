import type { Metadata } from "next";
import Link from "next/link";
import { ChevronRight, ReceiptText } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { INVOICE_PAGE_SIZE, invoicesHref, parseInvoiceSearch } from "@/lib/finance/invoices";
import { formatNaira, invoiceStatusLabel, invoiceStatusTone } from "@/lib/finance/rules";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { Pagination } from "@/components/ui/pagination";
import { TermPicker } from "@/components/assessment/term-picker";
import { GenerateInvoicesButton, InvoiceFilters } from "@/components/finance/invoice-filters";

export const metadata: Metadata = { title: "Invoices" };

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" });
const dueText = (iso: string | null) => (iso ? dateFormat.format(new Date(`${iso}T12:00:00Z`)) : "—");

/** E10 part 1: invoices per term with search and filters, and "Generate invoices" (Admin, Bursar). */
export default async function InvoicesPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Invoices" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const parsed = parseInvoiceSearch(sp);
  const { terms, selected } = await loadTerms(parsed.termId);
  if (!selected) {
    return (
      <>
        <PageHeader title="Invoices" />
        <Card>
          <EmptyState title="No terms yet">Set up the school calendar under Academics first.</EmptyState>
        </Card>
      </>
    );
  }
  const search = { ...parsed, termId: selected.id };
  const termLabel = `${selected.label} ${selected.session}`;

  const [invoices, classrooms, levels] = await Promise.all([
    load((api) =>
      api.GET("/api/finance/invoices", {
        params: { query: { termId: selected.id, classRoomId: search.classRoomId, status: search.status, search: search.search, page: search.page, pageSize: INVOICE_PAGE_SIZE } },
      }),
    ),
    load((api) => api.GET("/api/academics/classrooms")),
    load((api) => api.GET("/api/academics/class-levels")),
  ]);

  const filtered = Boolean(search.classRoomId || search.status || search.search);
  let body: React.ReactNode;
  if (!invoices.ok) body = <ProblemState problem={invoices.problem} />;
  else if (invoices.data.items.length === 0)
    body = (
      <EmptyState icon={ReceiptText} title={filtered ? "No invoices match" : `No invoices for ${termLabel} yet`}>
        {filtered ? "Try another search or filter." : "Set up fee items, then use “Generate invoices” to bill every student."}
      </EmptyState>
    );
  else
    body = (
      <>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[56rem] text-sm">
            <thead className="text-left text-xs uppercase tracking-wider text-muted">
              <tr className="border-b border-border">
                <th className="px-4 py-2.5 font-medium">Invoice</th>
                <th className="px-3 py-2.5 font-medium">Admission no.</th>
                <th className="px-3 py-2.5 font-medium">Student</th>
                <th className="px-3 py-2.5 font-medium">Class</th>
                <th className="px-3 py-2.5 text-right font-medium">Total</th>
                <th className="px-3 py-2.5 text-right font-medium">Paid</th>
                <th className="px-3 py-2.5 text-right font-medium">Balance</th>
                <th className="px-3 py-2.5 font-medium">Status</th>
                <th className="px-3 py-2.5 font-medium">Due</th>
                <th className="w-10 px-2" aria-label="Open" />
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {invoices.data.items.map((i) => (
                <tr key={i.id} className="hover:bg-surface-2/50">
                  <td className="tabular px-4 py-2.5 font-medium">
                    <Link href={`/admin/invoices/${i.id}`} className="text-brand hover:underline">
                      {i.number}
                    </Link>
                  </td>
                  <td className="tabular px-3 py-2.5">{i.admissionNumber}</td>
                  <td className="px-3 py-2.5">{i.studentName}</td>
                  <td className="px-3 py-2.5 text-muted">{i.classRoom ?? "—"}</td>
                  <td className="tabular px-3 py-2.5 text-right">{formatNaira(i.total)}</td>
                  <td className="tabular px-3 py-2.5 text-right">{formatNaira(i.paid)}</td>
                  <td className="tabular px-3 py-2.5 text-right font-medium">{formatNaira(i.balance)}</td>
                  <td className="px-3 py-2.5">
                    <Badge tone={invoiceStatusTone[i.status]}>{invoiceStatusLabel[i.status]}</Badge>
                  </td>
                  <td className="px-3 py-2.5 whitespace-nowrap text-muted">{dueText(i.dueOn)}</td>
                  <td className="px-2 py-2.5">
                    <Link href={`/admin/invoices/${i.id}`} aria-label={`Open invoice ${i.number}`} className="inline-flex size-8 items-center justify-center rounded-input text-muted hover:bg-surface-2 hover:text-text">
                      <ChevronRight className="size-4" aria-hidden />
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="border-t border-border px-4 py-3">
          <Pagination page={invoices.data.page} pageSize={invoices.data.pageSize} totalCount={invoices.data.totalCount} hrefFor={(page) => invoicesHref(search, { page })} noun="invoices" />
        </div>
      </>
    );

  return (
    <>
      <PageHeader
        title="Invoices"
        description="Each student's bill for the term. Search by name, admission number or invoice number."
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <TermPicker terms={terms} value={selected.id} />
            <GenerateInvoicesButton termId={selected.id} termLabel={termLabel} levels={levels.ok ? levels.data.map((l) => ({ id: l.id, name: l.name })) : []} />
          </div>
        }
      />
      <InvoiceFilters search={search} classrooms={classrooms.ok ? classrooms.data : []}>
        {body}
      </InvoiceFilters>
    </>
  );
}
