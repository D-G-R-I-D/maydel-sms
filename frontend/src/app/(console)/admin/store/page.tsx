import type { Metadata } from "next";
import Link from "next/link";
import { AlertTriangle, Plus, Shirt } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { formatNaira } from "@/lib/finance/rules";
import { siteImage } from "@/lib/site/images";
import { categories, lowestPrice, sectionsLabel, unpricedOptions } from "@/lib/store/store";
import { Badge, Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { StoreTabs } from "@/components/store/store-tabs";

export const metadata: Metadata = { title: "School store" };

/** E19 admin/bursar: everything the store sells, by category, with what's running low. */
export default async function StoreCataloguePage() {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin", "Bursar"])) {
    return (
      <>
        <PageHeader title="School store" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const [items, low] = await Promise.all([
    load((api) => api.GET("/api/store/items", { params: { query: { includeInactive: true } } })),
    load((api) => api.GET("/api/store/low-stock")),
  ]);
  const add = (
    <Link href="/admin/store/items/new" className={buttonClasses("primary")}>
      <Plus className="size-4" aria-hidden />
      Add item
    </Link>
  );

  return (
    <>
      <PageHeader title="School store" description="Uniforms, sportswear, books and ID cards: prices, stock, office sales and parents' orders." actions={add} />
      <StoreTabs active="catalogue" />

      {low.ok && low.data.length > 0 && (
        <Card className="mb-5 border-warning/40 bg-warning/5 p-4">
          <h2 className="flex items-center gap-2 text-sm font-semibold">
            <AlertTriangle className="size-4 text-warning" aria-hidden />
            Running low ({low.data.length})
          </h2>
          <ul className="mt-2 flex flex-wrap gap-2">
            {low.data.map((r) => (
              <li key={r.variantId}>
                <Link href={`/admin/store/items/${r.itemId}`} className="inline-flex min-h-8 items-center gap-2 rounded-full border border-border bg-surface px-3 text-sm hover:border-brand">
                  {r.item} · {r.variant}
                  <span className={r.stock === 0 ? "font-semibold text-danger" : "font-semibold text-warning"}>{r.stock === 0 ? "out" : r.stock}</span>
                </Link>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {!items.ok ? (
        <Card>
          <ProblemState problem={items.problem} />
        </Card>
      ) : items.data.length === 0 ? (
        <Card>
          <EmptyState icon={Shirt} title="Nothing in the store yet" action={add}>
            Add uniforms, sportswear, books or ID cards with their sizes and prices.
          </EmptyState>
        </Card>
      ) : (
        <div className="space-y-8">
          {categories
            .map((c) => ({ ...c, items: items.data.filter((i) => i.category === c.value) }))
            .filter((c) => c.items.length > 0)
            .map((c) => (
              <section key={c.value} aria-labelledby={`cat-${c.value}`}>
                <h2 id={`cat-${c.value}`} className="mb-3 font-display text-lg font-semibold">
                  {c.label}
                </h2>
                <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                  {c.items.map((i) => {
                    const src = siteImage(i.imageUrl);
                    const from = lowestPrice(i);
                    const unpriced = unpricedOptions(i.variants);
                    const stock = i.variants.filter((v) => v.isActive).reduce((t, v) => t + v.stock, 0);
                    const lowCount = i.variants.filter((v) => v.isActive && v.isLow).length;
                    return (
                      <li key={i.id}>
                        <Link href={`/admin/store/items/${i.id}`} className="card-interactive flex h-full gap-4 rounded-card border border-border bg-surface p-4">
                          <div className="flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-input bg-surface-2">
                            {src ? <img src={src} alt="" className="size-full object-cover" /> : <Shirt className="size-8 text-muted" aria-hidden />}
                          </div>
                          <div className="min-w-0 flex-1">
                            <p className="truncate font-medium">{i.name}</p>
                            <p className="text-sm text-muted">
                              {from != null ? `From ${formatNaira(from)}` : "Not priced"} · {i.variants.filter((v) => v.isActive).length} options
                            </p>
                            <p className="text-xs text-muted">
                              {stock} in stock · {sectionsLabel(i.sections)}
                            </p>
                            <div className="mt-2 flex flex-wrap gap-1.5">
                              {i.isActive ? <Badge tone="success">On sale</Badge> : <Badge>Hidden</Badge>}
                              {unpriced > 0 && <Badge tone="warning">{unpriced} unpriced</Badge>}
                              {lowCount > 0 && <Badge tone="danger">{lowCount} low</Badge>}
                            </div>
                          </div>
                        </Link>
                      </li>
                    );
                  })}
                </ul>
              </section>
            ))}
        </div>
      )}
    </>
  );
}
