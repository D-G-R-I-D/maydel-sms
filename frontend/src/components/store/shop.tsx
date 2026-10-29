"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Minus, Plus, Shirt, ShoppingBag, Trash2 } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { formatNaira } from "@/lib/finance/rules";
import { siteImage } from "@/lib/site/images";
import { placeShopOrder } from "@/lib/store/actions";
import { addToCart, cartTotal, categories, lowestPrice, type CartLine, type ShopCatalogue, type StoreItem } from "@/lib/store/store";
import { Button, Card, Select, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

/** One product: photo, description, option picker (sold-out options disabled) and Add. */
function ProductCard({ item, onAdd }: { item: StoreItem; onAdd: (line: CartLine) => void }) {
  const options = item.variants.filter((v) => v.isActive);
  const firstInStock = options.find((v) => v.stock > 0);
  const [chosen, setChosen] = useState(firstInStock?.id ?? options[0]?.id ?? "");
  const v = options.find((o) => o.id === chosen);
  const src = siteImage(item.imageUrl);
  const from = lowestPrice(item);
  const id = `opt-${item.id}`;
  return (
    <Card className="flex h-full flex-col overflow-hidden">
      <div className="flex aspect-[4/3] items-center justify-center bg-surface-2">
        {src ? <img src={src} alt={item.name} className="size-full object-cover" /> : <Shirt className="size-12 text-muted" aria-hidden />}
      </div>
      <div className="flex flex-1 flex-col gap-3 p-4">
        <div>
          <h3 className="font-medium">{item.name}</h3>
          {item.description && <p className="mt-0.5 text-sm text-muted">{item.description}</p>}
        </div>
        <div className="mt-auto space-y-2">
          {options.length > 1 ? (
            <div className="space-y-1">
              <label htmlFor={id} className="block text-xs font-medium text-muted">
                Size / option
              </label>
              <Select id={id} value={chosen} onChange={(e) => setChosen(e.target.value)} className="w-full">
                {options.map((o) => (
                  <option key={o.id} value={o.id} disabled={o.stock === 0}>
                    {o.label} · {o.stock === 0 ? "sold out" : formatNaira(o.price)}
                  </option>
                ))}
              </Select>
            </div>
          ) : (
            v && <p className="text-sm text-muted">{v.label}</p>
          )}
          <div className="flex items-center justify-between gap-3">
            <span className="tabular font-semibold">{v ? formatNaira(v.price) : from != null ? formatNaira(from) : ""}</span>
            <Button
              size="sm"
              className="whitespace-nowrap"
              disabled={!v || v.stock === 0}
              onClick={() => v && onAdd({ variantId: v.id, item: item.name, variant: v.label, price: v.price, quantity: 1, stock: v.stock })}
            >
              {v && v.stock === 0 ? "Sold out" : "Add"}
            </Button>
          </div>
          {v && v.stock > 0 && v.stock <= 3 && <p className="text-xs text-warning">Only {v.stock} left</p>}
        </div>
      </div>
    </Card>
  );
}

/** E19 portal shop: the child's items, a basket, then "pay at the office" or "add to fees". */
export function Shop({ catalogue }: { catalogue: ShopCatalogue }) {
  const router = useRouter();
  const [lines, setLines] = useState<CartLine[]>([]);
  const [payment, setPayment] = useState<"AtOffice" | "Fees">("AtOffice");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const total = cartTotal(lines);
  const count = lines.reduce((t, l) => t + l.quantity, 0);
  const setQty = (id: string, q: number) => setLines((ls) => ls.map((l) => (l.variantId === id ? { ...l, quantity: Math.max(1, Math.min(q, l.stock)) } : l)));
  const first = catalogue.student.split(" ")[0];
  const groups = categories.map((c) => ({ ...c, items: catalogue.items.filter((i) => i.category === c.value) })).filter((g) => g.items.length > 0);

  const payOptions: { value: "AtOffice" | "Fees"; label: string; hint: string }[] = [
    { value: "AtOffice", label: "Pay at the office", hint: `We hold it for ${catalogue.holdHours} hours. Pay when you collect.` },
    ...(catalogue.canChargeToFees ? [{ value: "Fees" as const, label: "Add to school fees", hint: `Goes on ${first}'s invoice for this term.` }] : []),
  ];

  const place = async () => {
    setBusy(true);
    setProblem(null);
    const order = await runAction(() => placeShopOrder(catalogue.studentId, lines.map((l) => ({ variantId: l.variantId, quantity: l.quantity })), payment), {
      success: (o) => `Order ${o.number} placed`,
      onProblem: setProblem,
    });
    setBusy(false);
    if (order) router.push(`/portal/shop/orders/${order.id}`);
  };

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_20rem] lg:items-start">
      <div className="space-y-8">
        {groups.map((g) => (
          <section key={g.value} aria-labelledby={`shop-${g.value}`}>
            <h2 id={`shop-${g.value}`} className="mb-3 font-display text-lg font-semibold">
              {g.label}
            </h2>
            <ul className="grid gap-4 sm:grid-cols-2">
              {g.items.map((i) => (
                <li key={i.id}>
                  <ProductCard item={i} onAdd={(l) => setLines((ls) => addToCart(ls, l))} />
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>

      <Card className="space-y-4 p-5 lg:sticky lg:top-20" aria-labelledby="basket-title">
        <h2 id="basket-title" className="flex items-center gap-2 font-display text-lg font-semibold">
          <ShoppingBag className="size-5 text-brand" aria-hidden />
          Basket{count > 0 ? ` (${count})` : ""}
        </h2>
        {problem && <Alert tone="danger">{problemMessage(problem)}</Alert>}
        {lines.length === 0 ? (
          <p className="text-sm text-muted">Add items for {first} and they&apos;ll show here.</p>
        ) : (
          <>
            <ul className="divide-y divide-border">
              {lines.map((l) => (
                <li key={l.variantId} className="flex items-center gap-1.5 py-2 text-sm">
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{l.item}</p>
                    <p className="text-xs text-muted">
                      {l.variant} · {formatNaira(l.price * l.quantity)}
                    </p>
                  </div>
                  <Button variant="ghost" size="sm" aria-label={`One fewer ${l.item} ${l.variant}`} disabled={l.quantity <= 1} onClick={() => setQty(l.variantId, l.quantity - 1)}>
                    <Minus className="size-4" aria-hidden />
                  </Button>
                  <span className="tabular w-5 text-center">{l.quantity}</span>
                  <Button variant="ghost" size="sm" aria-label={`One more ${l.item} ${l.variant}`} disabled={l.quantity >= l.stock} onClick={() => setQty(l.variantId, l.quantity + 1)}>
                    <Plus className="size-4" aria-hidden />
                  </Button>
                  <Button variant="ghost" size="sm" aria-label={`Remove ${l.item} ${l.variant}`} onClick={() => setLines((ls) => ls.filter((x) => x.variantId !== l.variantId))}>
                    <Trash2 className="size-4" aria-hidden />
                  </Button>
                </li>
              ))}
            </ul>
            <div className="flex items-center justify-between border-t border-border pt-2 font-semibold">
              <span>Total</span>
              <span className="tabular">{formatNaira(total)}</span>
            </div>
            <fieldset className="space-y-2">
              <legend className="text-sm font-medium">How will you pay?</legend>
              {payOptions.map((o) => (
                <label
                  key={o.value}
                  htmlFor={`shop-pay-${o.value}`}
                  className={cn("grid cursor-pointer grid-cols-[auto_1fr] items-start gap-x-3 rounded-input border p-3 text-sm font-medium", payment === o.value ? "border-brand bg-brand/5" : "border-border")}
                >
                  <input id={`shop-pay-${o.value}`} type="radio" name="shop-pay" value={o.value} checked={payment === o.value} onChange={() => setPayment(o.value)} aria-describedby={`shop-pay-${o.value}-hint`} className="mt-0.5 size-4 accent-brand" />
                  {o.label}
                  <span id={`shop-pay-${o.value}-hint`} className="col-start-2 font-normal text-muted">
                    {o.hint}
                  </span>
                </label>
              ))}
            </fieldset>
            <Button className="w-full" size="lg" loading={busy} loadingText="Placing order…" onClick={place}>
              Place order · {formatNaira(total)}
            </Button>
            <p className="text-xs text-muted">The school tells you when it&apos;s ready to collect.</p>
          </>
        )}
      </Card>
    </div>
  );
}
