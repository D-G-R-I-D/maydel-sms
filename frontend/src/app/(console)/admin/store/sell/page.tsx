import type { Metadata } from "next";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Card, PageHeader } from "@/components/ui/primitives";
import { AccessDenied, ProblemState } from "@/components/ui/states";
import { StoreTabs } from "@/components/store/store-tabs";
import { SellForm } from "@/components/store/sell-form";

export const metadata: Metadata = { title: "Sell" };

/** E19 office till: a sale to a student, paid now or added to fees. */
export default async function StoreSellPage() {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="Sell" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const items = await load((api) => api.GET("/api/store/items"));
  return (
    <>
      <PageHeader title="School store" description="Sell at the office: choose the student, add items, take payment." />
      <StoreTabs active="sell" />
      {items.ok ? (
        <SellForm items={items.data.filter((i) => i.isActive && i.variants.some((v) => v.isActive))} />
      ) : (
        <Card>
          <ProblemState problem={items.problem} />
        </Card>
      )}
    </>
  );
}
