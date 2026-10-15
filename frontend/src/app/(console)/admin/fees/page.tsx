import type { Metadata } from "next";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { loadTerms } from "@/lib/assessment/terms";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { TermPicker } from "@/components/assessment/term-picker";
import { FeeItems } from "@/components/finance/fee-items";

export const metadata: Metadata = { title: "Fee items" };

/** E10 part 1: fee items per term (Admin, Bursar). */
export default async function FeeItemsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Fee items" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const { terms, selected } = await loadTerms(Array.isArray(sp.termId) ? sp.termId[0] : sp.termId);
  const header = (
    <PageHeader
      title="Fee items"
      description="What each term costs: tuition and other fees, school-wide or per class level. Optional items are billed only to the students who take them."
      actions={selected ? <TermPicker terms={terms} value={selected.id} /> : undefined}
    />
  );
  if (!selected) {
    return (
      <>
        {header}
        <Card>
          <EmptyState title="No terms yet">Set up the school calendar under Academics first.</EmptyState>
        </Card>
      </>
    );
  }

  const [items, levels] = await Promise.all([
    load((api) => api.GET("/api/finance/fee-items", { params: { query: { termId: selected.id } } })),
    load((api) => api.GET("/api/academics/class-levels")),
  ]);
  const failed = !items.ok ? items.problem : !levels.ok ? levels.problem : null;
  if (failed || !items.ok || !levels.ok) {
    return (
      <>
        {header}
        <Card>{failed && <ProblemState problem={failed} />}</Card>
      </>
    );
  }
  return (
    <>
      {header}
      <FeeItems
        termId={selected.id}
        termLabel={`${selected.label} ${selected.session}`}
        items={items.data}
        levels={levels.data.map((l) => ({ id: l.id, name: l.name }))}
        otherTerms={terms.filter((t) => t.id !== selected.id).map((t) => ({ id: t.id, label: `${t.label} ${t.session}` }))}
      />
    </>
  );
}
