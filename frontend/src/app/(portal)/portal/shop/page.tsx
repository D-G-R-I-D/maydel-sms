import type { Metadata } from "next";
import Link from "next/link";
import { ChevronRight, ShoppingBag, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { formatNaira } from "@/lib/finance/rules";
import { formatLagos } from "@/lib/time/lagos";
import { statusLabel, statusTone } from "@/lib/store/store";
import { ChildSwitcher } from "@/components/portal/child-switcher";
import { Shop } from "@/components/store/shop";
import { Badge, Card, PageHeader } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "School shop" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E19 portal: order uniforms, books and ID cards for a child; collect at the office. */
export default async function PortalShopPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const header = <PageHeader title="School shop" description="Order uniforms, sportswear and books, then collect them from the school office." />;
  if (!hasAnyRole(user.roles, ["Parent"])) {
    return (
      <>
        {header}
        <Card>
          <EmptyState icon={ShoppingBag} title="For parents">
            Ask your parent or guardian to order from their portal account.
          </EmptyState>
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const children = await load((api) => api.GET("/api/portal/children"));
  if (!children.ok || children.data.length === 0) {
    return (
      <>
        {header}
        <Card>
          {children.ok ? (
            <EmptyState icon={Users} title="No children linked yet">
              Ask the school office to link your children to your account.
            </EmptyState>
          ) : (
            <ProblemState problem={children.problem} />
          )}
        </Card>
      </>
    );
  }
  const child = children.data.find((c) => c.studentId === one(sp.child)) ?? children.data[0];
  const path = { params: { path: { studentId: child.studentId } } };
  const [catalogue, orders] = await Promise.all([
    load((api) => api.GET("/api/portal/children/{studentId}/shop", path)),
    load((api) => api.GET("/api/portal/children/{studentId}/shop/orders", path)),
  ]);
  const recent = orders.ok ? orders.data.slice(0, 5) : [];

  return (
    <>
      {header}
      <ChildSwitcher items={children.data} activeId={child.studentId} basePath="/portal/shop" />

      {recent.length > 0 && (
        <Card className="mb-6 p-4">
          <h2 className="text-sm font-semibold">{child.name.split(" ")[0]}&apos;s orders</h2>
          <ul className="mt-2 divide-y divide-border">
            {recent.map((o) => (
              <li key={o.id}>
                <Link href={`/portal/shop/orders/${o.id}`} className="flex items-center gap-3 py-2 text-sm hover:text-brand">
                  <span className="tabular font-medium">{o.number}</span>
                  <span className="hidden text-muted sm:inline">{formatLagos(o.createdAt)}</span>
                  <Badge tone={statusTone[o.status]}>{statusLabel[o.status]}</Badge>
                  <span className="tabular ml-auto">{formatNaira(o.total)}</span>
                  <ChevronRight className="size-4 text-muted" aria-hidden />
                </Link>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {!catalogue.ok ? (
        <Card>
          <ProblemState problem={catalogue.problem} />
        </Card>
      ) : catalogue.data.items.length === 0 ? (
        <Card>
          <EmptyState icon={ShoppingBag} title="Nothing to order right now">
            The school hasn&apos;t put items on sale for {child.name.split(" ")[0]}&apos;s section yet.
          </EmptyState>
        </Card>
      ) : (
        <Shop key={child.studentId} catalogue={catalogue.data} />
      )}
    </>
  );
}
