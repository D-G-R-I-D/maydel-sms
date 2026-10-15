import type { Metadata } from "next";
import { Eye } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TermPicker } from "@/components/assessment/term-picker";
import { InstalmentPlanForm, SiblingDiscountForm } from "@/components/finance/fee-settings";

export const metadata: Metadata = { title: "Fee settings" };

/** E10 part 1: sibling discount and instalment plans. Admin edits; bursar sees them read-only. */
export default async function FeeSettingsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Fees" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const isAdmin = hasAnyRole(user.roles, ["Admin"]);
  const sp = await searchParams;
  const { terms, selected } = await loadTerms(Array.isArray(sp.termId) ? sp.termId[0] : sp.termId);
  const [settings, plan] = await Promise.all([
    load((api) => api.GET("/api/finance/settings")),
    selected ? load((api) => api.GET("/api/finance/instalment-plan", { params: { query: { termId: selected.id } } })) : Promise.resolve(null),
  ]);

  return (
    <>
      <PageHeader
        title="Fees"
        description="Sibling discount and instalment plans. Changes apply to invoices generated afterwards."
        actions={selected ? <TermPicker terms={terms} value={selected.id} /> : undefined}
      />
      {!isAdmin && (
        <p className="-mt-3 mb-5 flex items-center gap-1.5 text-sm text-muted">
          <Eye className="size-4" aria-hidden />
          View only. Ask an administrator to change these.
        </p>
      )}
      <div className="grid gap-5 xl:grid-cols-2">
        {settings.ok ? (
          <SiblingDiscountForm initial={settings.data} canEdit={isAdmin} />
        ) : (
          <Card>
            <ProblemState problem={settings.problem} />
          </Card>
        )}
        {!selected || !plan ? (
          <Card>
            <EmptyState title="No terms yet">Set up the school calendar first.</EmptyState>
          </Card>
        ) : plan.ok ? (
          <InstalmentPlanForm key={selected.id} termId={selected.id} termLabel={`${selected.label} ${selected.session}`} initial={plan.data} canEdit={isAdmin} />
        ) : (
          <Card>
            <ProblemState problem={plan.problem} />
          </Card>
        )}
      </div>
    </>
  );
}
