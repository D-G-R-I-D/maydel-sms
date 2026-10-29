"use client";

import { useEffect, useState } from "react";
import { Minus, Plus, Search, ShoppingCart, Trash2, X } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { formatNaira } from "@/lib/finance/rules";
import { searchStudentsForSale, sellInStore } from "@/lib/store/actions";
import { addToCart, cartTotal, categoryLabel, officeMethods, type CartLine, type PayMethod, type StoreItem, type StoreOrder } from "@/lib/store/store";
import { Button, Card, Field, Input, Select, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";
import { PdfDownloadButton } from "@/components/reports/pdf-download";

type Student = { id: string; name: string; admissionNumber: string; classRoom: string | null };

function StudentPicker({ value, onChange }: { value: Student | null; onChange: (s: Student | null) => void }) {
  const [q, setQ] = useState("");
  const [results, setResults] = useState<Student[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (q.trim().length < 2) return;
    let live = true;
    const t = setTimeout(async () => {
      try {
        const r = await searchStudentsForSale(q);
        if (!live) return;
        setError(r.ok ? null : problemMessage(r.problem));
        setResults(r.ok ? r.data : []);
      } catch {
        if (live) setError("Couldn't search. Check your connection.");
      }
    }, 250);
    return () => {
      live = false;
      clearTimeout(t);
    };
  }, [q]);

  if (value)
    return (
      <div className="flex items-center justify-between gap-3 rounded-input border border-brand bg-brand/5 px-3 py-2">
        <div className="min-w-0">
          <p className="truncate font-medium">{value.name}</p>
          <p className="text-xs text-muted">
            {value.admissionNumber}
            {value.classRoom ? ` · ${value.classRoom}` : ""}
          </p>
        </div>
        <Button variant="ghost" size="sm" aria-label="Change student" onClick={() => onChange(null)}>
          <X className="size-4" aria-hidden />
        </Button>
      </div>
    );

  const shown = q.trim().length < 2 ? [] : results;
  return (
    <div>
      <div className="relative">
        <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
        <Input id="sell-student" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Name or admission number" className="pl-9" autoComplete="off" />
      </div>
      {error && <p className="mt-1.5 text-sm text-danger">{error}</p>}
      {shown.length > 0 && (
        <ul className="mt-2 divide-y divide-border rounded-input border border-border" aria-label="Matching students">
          {shown.map((s) => (
            <li key={s.id}>
              <button type="button" onClick={() => onChange(s)} className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm hover:bg-surface-2">
                <span className="font-medium">{s.name}</span>
                <span className="text-xs text-muted">
                  {s.admissionNumber}
                  {s.classRoom ? ` · ${s.classRoom}` : ""}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {q.trim().length >= 2 && results.length === 0 && !error && <p className="mt-1.5 text-sm text-muted">No active student found yet…</p>}
    </div>
  );
}

/** One product with its options as add buttons; sold-out options are disabled. */
function ProductRow({ item, onAdd }: { item: StoreItem; onAdd: (line: CartLine) => void }) {
  const options = item.variants.filter((v) => v.isActive);
  return (
    <li className="py-3">
      <p className="text-sm font-medium">
        {item.name} <span className="font-normal text-muted">· {categoryLabel(item.category)}</span>
      </p>
      <div className="mt-2 flex flex-wrap gap-2">
        {options.map((v) => (
          <button
            key={v.id}
            type="button"
            disabled={v.stock === 0}
            onClick={() => onAdd({ variantId: v.id, item: item.name, variant: v.label, price: v.price, quantity: 1, stock: v.stock })}
            className="inline-flex min-h-9 items-center gap-2 rounded-full border border-border px-3 text-sm hover:border-brand hover:bg-brand/5 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <span>{v.label}</span>
            <span className="tabular text-muted">{v.stock === 0 ? "Sold out" : formatNaira(v.price)}</span>
          </button>
        ))}
      </div>
    </li>
  );
}

/** E19 office till: pick a student, add items, take payment (or put it on fees), print the receipt. */
export function SellForm({ items }: { items: StoreItem[] }) {
  const [student, setStudent] = useState<Student | null>(null);
  const [lines, setLines] = useState<CartLine[]>([]);
  const [method, setMethod] = useState<PayMethod>("Cash");
  const [reference, setReference] = useState("");
  const [handedOver, setHandedOver] = useState(true);
  const [filter, setFilter] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [done, setDone] = useState<StoreOrder | null>(null);

  const total = cartTotal(lines);
  const f = filter.trim().toLowerCase();
  const shown = f ? items.filter((i) => i.name.toLowerCase().includes(f) || categoryLabel(i.category).toLowerCase().includes(f)) : items;
  const setQty = (id: string, q: number) => setLines((ls) => ls.map((l) => (l.variantId === id ? { ...l, quantity: Math.max(1, Math.min(q, l.stock)) } : l)));

  const reset = () => {
    setStudent(null);
    setLines([]);
    setMethod("Cash");
    setReference("");
    setHandedOver(true);
    setProblem(null);
    setDone(null);
  };

  const sell = async () => {
    if (!student || lines.length === 0) return;
    setBusy(true);
    setProblem(null);
    const r = await runAction(
      () => sellInStore(student.id, lines.map((l) => ({ variantId: l.variantId, quantity: l.quantity })), method, method === "Fees" ? null : reference.trim() || null, handedOver),
      { success: (o) => `Sale ${o.number} recorded`, onProblem: setProblem },
    );
    setBusy(false);
    if (r) setDone(r);
  };

  if (done)
    return (
      <Card className="mx-auto max-w-lg space-y-4 p-6 text-center">
        <ShoppingCart className="mx-auto size-10 text-success" aria-hidden />
        <div>
          <h2 className="font-display text-xl font-semibold">Sale {done.number} recorded</h2>
          <p className="mt-1 text-sm text-muted">
            {done.student} · {formatNaira(done.total)} · {done.payment === "ChargedToFees" ? `added to fees${done.invoiceNumber ? ` (${done.invoiceNumber})` : ""}` : "paid"}
            {done.status === "Collected" ? " · handed over" : " · waiting in Orders"}
          </p>
        </div>
        <div className="flex flex-wrap justify-center gap-2">
          <PdfDownloadButton href={`/api/media/store/orders/${done.id}/receipt`} fallbackName={`receipt-${done.number}.pdf`} label="Receipt" icon="print" />
          <Button onClick={reset}>New sale</Button>
        </div>
      </Card>
    );

  return (
    <div className="grid gap-5 lg:grid-cols-[1.3fr_1fr] lg:items-start">
      <Card className="p-5">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <h2 className="font-display text-lg font-semibold">Items</h2>
          <div className="w-full sm:w-56">
            <label htmlFor="sell-filter" className="sr-only">
              Filter items
            </label>
            <Input id="sell-filter" value={filter} onChange={(e) => setFilter(e.target.value)} placeholder="Filter items" />
          </div>
        </div>
        {shown.length === 0 ? (
          <p className="mt-4 text-sm text-muted">{items.length === 0 ? "Nothing is on sale yet. Put items on sale in the Catalogue." : "No item matches."}</p>
        ) : (
          <ul className="mt-2 divide-y divide-border">
            {shown.map((i) => (
              <ProductRow key={i.id} item={i} onAdd={(l) => setLines((ls) => addToCart(ls, l))} />
            ))}
          </ul>
        )}
      </Card>

      <Card className="space-y-4 p-5 lg:sticky lg:top-20">
        {problem && <Alert tone="danger">{problemMessage(problem)}</Alert>}
        <div className="space-y-1.5">
          <label htmlFor="sell-student" className="block text-sm font-medium">
            Student
          </label>
          <StudentPicker value={student} onChange={setStudent} />
        </div>

        <div>
          <h3 className="text-sm font-medium">Basket</h3>
          {lines.length === 0 ? (
            <p className="mt-1 text-sm text-muted">Tap an option on the left to add it.</p>
          ) : (
            <ul className="mt-2 divide-y divide-border">
              {lines.map((l) => (
                <li key={l.variantId} className="flex items-center gap-2 py-2 text-sm">
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{l.item}</p>
                    <p className="text-xs text-muted">
                      {l.variant} · {formatNaira(l.price)}
                    </p>
                  </div>
                  <Button variant="ghost" size="sm" aria-label={`One fewer ${l.item} ${l.variant}`} disabled={l.quantity <= 1} onClick={() => setQty(l.variantId, l.quantity - 1)}>
                    <Minus className="size-4" aria-hidden />
                  </Button>
                  <span className="tabular w-6 text-center" aria-label={`Quantity ${l.quantity}`}>
                    {l.quantity}
                  </span>
                  <Button variant="ghost" size="sm" aria-label={`One more ${l.item} ${l.variant}`} disabled={l.quantity >= l.stock} onClick={() => setQty(l.variantId, l.quantity + 1)}>
                    <Plus className="size-4" aria-hidden />
                  </Button>
                  <span className="tabular w-20 text-right">{formatNaira(l.price * l.quantity)}</span>
                  <Button variant="ghost" size="sm" aria-label={`Remove ${l.item} ${l.variant}`} onClick={() => setLines((ls) => ls.filter((x) => x.variantId !== l.variantId))}>
                    <Trash2 className="size-4" aria-hidden />
                  </Button>
                </li>
              ))}
            </ul>
          )}
          <div className="mt-2 flex items-center justify-between border-t border-border pt-2 font-semibold">
            <span>Total</span>
            <span className="tabular">{formatNaira(total)}</span>
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <Field id="sell-method" label="Payment">
            <Select id="sell-method" value={method ?? "Cash"} onChange={(e) => setMethod(e.target.value as PayMethod)}>
              {officeMethods.map((m) => (
                <option key={m.value} value={m.value ?? ""}>
                  {m.label}
                </option>
              ))}
            </Select>
          </Field>
          {method !== "Fees" && (
            <Field id="sell-ref" label="Reference (optional)">
              <Input id="sell-ref" value={reference} onChange={(e) => setReference(e.target.value)} maxLength={100} />
            </Field>
          )}
        </div>
        {method === "Fees" && <p className="text-sm text-muted">Added to the student&apos;s invoice for the current term.</p>}
        <div className={cn("flex items-center gap-3 text-sm")}>
          <Switch id="sell-handed" label="Handed over now" checked={handedOver} onCheckedChange={setHandedOver} />
          <span>{handedOver ? "Handed over now" : "Collect later (goes to Orders)"}</span>
        </div>
        <Button className="w-full" size="lg" disabled={!student || lines.length === 0} loading={busy} loadingText="Recording…" onClick={sell}>
          {student ? `Sell ${formatNaira(total)} to ${student.name.split(" ")[0]}` : "Choose a student"}
        </Button>
      </Card>
    </div>
  );
}
