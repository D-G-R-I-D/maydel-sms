import { ReceiptText } from "lucide-react";
import type { StudentStatement } from "@/lib/api/types";
import { formatNaira } from "@/lib/finance/rules";
import { Badge, Card, cn } from "@/components/ui/primitives";
import { EmptyState } from "@/components/ui/states";

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" });
const day = (iso: string) => dateFormat.format(new Date(`${iso.slice(0, 10)}T12:00:00Z`));
const kindTone: Record<string, "neutral" | "success" | "danger"> = { Invoice: "neutral", Payment: "success", Reversal: "danger" };

/** E10 part 2a: a student's fee statement: invoices (debits), payments (credits), reversals, running balance. */
export function StudentStatementView({ statement }: { statement: StudentStatement }) {
  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-3">
        <Card className="p-4">
          <p className="text-sm text-muted">Billed</p>
          <p className="tabular font-display text-2xl font-semibold">{formatNaira(statement.totalBilled)}</p>
        </Card>
        <Card className="p-4">
          <p className="text-sm text-muted">Paid</p>
          <p className="tabular font-display text-2xl font-semibold">{formatNaira(statement.totalPaid)}</p>
        </Card>
        <Card className="p-4">
          <p className="text-sm text-muted">Balance</p>
          <p className={cn("tabular font-display text-2xl font-semibold", statement.balance > 0 ? "text-danger" : "text-success")}>{formatNaira(statement.balance)}</p>
        </Card>
      </div>
      <Card className="overflow-hidden">
        {statement.entries.length === 0 ? (
          <EmptyState icon={ReceiptText} title="Nothing billed yet">
            Invoices and payments for {statement.name} will appear here.
          </EmptyState>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[44rem] text-sm">
              <thead className="text-left text-xs uppercase tracking-wider text-muted">
                <tr className="border-b border-border">
                  <th className="px-4 py-2.5 font-medium">Date</th>
                  <th className="px-3 py-2.5 font-medium">Reference</th>
                  <th className="px-3 py-2.5 font-medium">Description</th>
                  <th className="px-3 py-2.5 text-right font-medium">Debit</th>
                  <th className="px-3 py-2.5 text-right font-medium">Credit</th>
                  <th className="px-4 py-2.5 text-right font-medium">Balance</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {statement.entries.map((e, i) => (
                  <tr key={`${e.reference}-${i}`}>
                    <td className="px-4 py-2.5 whitespace-nowrap">{day(e.date)}</td>
                    <td className="px-3 py-2.5">
                      <span className="tabular font-medium">{e.reference}</span>
                      <Badge tone={kindTone[e.kind] ?? "neutral"} className="ml-2">
                        {e.kind}
                      </Badge>
                    </td>
                    <td className="px-3 py-2.5 text-muted">{e.description}</td>
                    <td className="tabular px-3 py-2.5 text-right">{e.debit ? formatNaira(e.debit) : ""}</td>
                    <td className="tabular px-3 py-2.5 text-right text-success">{e.credit ? formatNaira(e.credit) : ""}</td>
                    <td className="tabular px-4 py-2.5 text-right font-medium">{formatNaira(e.balance)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}
