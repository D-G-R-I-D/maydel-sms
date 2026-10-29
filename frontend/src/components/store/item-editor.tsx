"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Plus, Trash2 } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { saveStoreItem } from "@/lib/store/actions";
import { categories, formatSections, parseSections, storeSections, unpricedOptions, type StoreCategory, type StoreItem, type StoreSection } from "@/lib/store/store";
import { Button, Card, Field, Input, Select, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";
import { AdjustDialog, MovementsDialog, RestockDialog } from "./stock-dialogs";

type Row = { key: string; id: string | null; label: string; price: string; lowStockAt: string; isActive: boolean };

let seq = 0;
const blankRow = (): Row => ({ key: `n${++seq}`, id: null, label: "", price: "", lowStockAt: "5", isActive: true });

const priceOk = (p: string) => /^\d+(\.\d{1,2})?$/.test(p) && Number(p) > 0;

/**
 * E19 admin/bursar: an item and its options (sizes, editions). Prices can be filled in later: the item
 * can't go on sale until every active option has one. Stock moves only through Restock/Adjust.
 */
export function ItemEditor({ item }: { item: StoreItem | null }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [name, setName] = useState(item?.name ?? "");
  const [category, setCategory] = useState<StoreCategory>(item?.category ?? "Uniform");
  const [description, setDescription] = useState(item?.description ?? "");
  const [sections, setSections] = useState<StoreSection[]>(item ? parseSections(item.sections) : []);
  const [isActive, setIsActive] = useState(item?.isActive ?? false);
  const [rows, setRows] = useState<Row[]>(
    item?.variants.length
      ? item.variants.map((v) => ({ key: v.id, id: v.id, label: v.label, price: v.price > 0 ? String(v.price) : "", lowStockAt: String(v.lowStockAt), isActive: v.isActive }))
      : [blankRow()],
  );
  const [busy, setBusy] = useState(false);
  const [tried, setTried] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);

  const nameErr = name.trim() ? undefined : "Give the item a name.";
  const rowErr = (r: Row) => ({
    label: r.label.trim() ? undefined : "Name the option, e.g. Age 6 or One size.",
    price: r.price === "" || priceOk(r.price) ? undefined : "An amount in naira.",
    low: /^\d{1,4}$/.test(r.lowStockAt) ? undefined : "A whole number.",
  });
  const labels = rows.map((r) => r.label.trim().toLowerCase());
  const dupes = labels.some((l, i) => l && labels.indexOf(l) !== i);
  const unpriced = unpricedOptions(rows.map((r) => ({ isActive: r.isActive, price: priceOk(r.price) ? Number(r.price) : 0 })));
  const invalid = Boolean(nameErr) || rows.length === 0 || dupes || rows.some((r) => Object.values(rowErr(r)).some(Boolean));
  const variantsById = new Map(item?.variants.map((v) => [v.id, v]) ?? []);
  const update = (key: string, patch: Partial<Row>) => setRows((rs) => rs.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  const toggleSection = (s: StoreSection) => setSections((list) => (list.includes(s) ? list.filter((x) => x !== s) : [...list, s]));

  const save = async () => {
    setTried(true);
    if (invalid) return;
    setBusy(true);
    setProblem(null);
    const saved = await runAction(
      () =>
        saveStoreItem(item?.id ?? null, {
          name: name.trim(),
          category,
          description: description.trim() || null,
          sections: formatSections(sections),
          isActive,
          variants: rows.map((r) => ({ id: r.id, label: r.label.trim(), price: priceOk(r.price) ? Number(r.price) : 0, lowStockAt: Number(r.lowStockAt), isActive: r.isActive })),
        }),
      { success: item ? "Item saved" : "Item added", onProblem: setProblem },
    );
    setBusy(false);
    if (!saved) return;
    if (!item) router.push(`/admin/store/items/${saved.id}`);
    else startTransition(() => router.refresh());
  };

  return (
    <div className="space-y-5">
      {problem && <Alert tone="danger">{problemMessage(problem)}</Alert>}
      <Card className="space-y-4 p-5">
        <div className="grid gap-4 sm:grid-cols-[2fr_1fr]">
          <Field id="si-name" label="Item name" error={tried ? nameErr : undefined}>
            <Input value={name} onChange={(e) => setName(e.target.value)} maxLength={120} placeholder="e.g. Shirt (white)" {...describedBy("si-name", tried ? nameErr : undefined)} />
          </Field>
          <Field id="si-cat" label="Category">
            <Select id="si-cat" value={category} onChange={(e) => setCategory(e.target.value as StoreCategory)}>
              {categories.map((c) => (
                <option key={c.value} value={c.value}>
                  {c.label}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <Field id="si-desc" label="Description (optional)" hint="Shown to parents in the shop.">
          <textarea
            id="si-desc"
            aria-describedby="si-desc-hint"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={1000}
            rows={3}
            className="w-full rounded-input border border-border bg-surface px-3 py-2 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
          />
        </Field>
        <fieldset>
          <legend className="text-sm font-medium">Who it&apos;s for</legend>
          <p className="mt-0.5 text-xs text-muted">Leave all unticked for everyone. Parents only see items for their child&apos;s section.</p>
          <div className="mt-2 flex flex-wrap gap-2">
            {storeSections.map((s) => (
              <label key={s.value} className={cn("inline-flex min-h-9 cursor-pointer items-center gap-2 rounded-full border px-3 text-sm", sections.includes(s.value) ? "border-brand bg-brand/10 text-brand" : "border-border")}>
                <input type="checkbox" className="size-4 accent-brand" checked={sections.includes(s.value)} onChange={() => toggleSection(s.value)} />
                {s.label}
              </label>
            ))}
          </div>
        </fieldset>
      </Card>

      <Card className="p-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="font-display text-lg font-semibold">Options</h2>
            <p className="text-sm text-muted">Sizes, editions or one option for a single item.{item ? " Use the buttons to restock or correct stock." : " Add stock after saving."}</p>
          </div>
          <Button variant="secondary" size="sm" onClick={() => setRows((rs) => [...rs, blankRow()])}>
            <Plus className="size-4" aria-hidden />
            Add option
          </Button>
        </div>
        {dupes && tried && (
          <Alert tone="danger" className="mt-3">
            Two options have the same name.
          </Alert>
        )}
        <ul className="mt-4 divide-y divide-border">
          {rows.map((r, i) => {
            const err = tried ? rowErr(r) : { label: undefined, price: r.price === "" || priceOk(r.price) ? undefined : "An amount in naira.", low: undefined };
            const saved = r.id ? variantsById.get(r.id) : undefined;
            const id = `sv-${r.key}`;
            return (
              <li key={r.key} className="grid gap-3 py-3 sm:grid-cols-[1.4fr_1fr_0.7fr_auto] sm:items-start">
                <Field id={`${id}-l`} label={`Option ${i + 1}`} error={err.label}>
                  <Input value={r.label} onChange={(e) => update(r.key, { label: e.target.value })} maxLength={60} placeholder="e.g. Age 6" {...describedBy(`${id}-l`, err.label)} />
                </Field>
                <Field id={`${id}-p`} label="Price (₦)" error={err.price}>
                  <Input value={r.price} onChange={(e) => update(r.key, { price: e.target.value })} inputMode="decimal" className="tabular" placeholder="Not priced" {...describedBy(`${id}-p`, err.price)} />
                </Field>
                <Field id={`${id}-w`} label="Warn below" error={err.low}>
                  <Input value={r.lowStockAt} onChange={(e) => update(r.key, { lowStockAt: e.target.value })} inputMode="numeric" className="tabular" {...describedBy(`${id}-w`, err.low)} />
                </Field>
                <div className="flex flex-wrap items-center gap-1 sm:pt-7">
                  {saved ? (
                    <>
                      <span className={cn("tabular mr-1 min-w-16 text-sm", saved.stock === 0 ? "text-danger" : saved.isLow ? "text-warning" : "text-muted")}>{saved.stock} in stock</span>
                      <RestockDialog variant={saved} label={`${name || "Item"} · ${saved.label}`} />
                      <AdjustDialog variant={saved} label={`${name || "Item"} · ${saved.label}`} />
                      <MovementsDialog variant={saved} label={`${name || "Item"} · ${saved.label}`} />
                    </>
                  ) : (
                    <span className="mr-1 text-sm text-muted">New</span>
                  )}
                  <Switch label={`Offer ${r.label || `option ${i + 1}`}`} checked={r.isActive} onCheckedChange={(v) => update(r.key, { isActive: v })} />
                  {!r.id && rows.length > 1 && (
                    <Button variant="ghost" size="sm" aria-label={`Remove option ${i + 1}`} onClick={() => setRows((rs) => rs.filter((x) => x.key !== r.key))}>
                      <Trash2 className="size-4" aria-hidden />
                    </Button>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
        <p className="mt-2 text-xs text-muted">Switch an option off to stop selling it; its stock history is kept.</p>
      </Card>

      <Card className="flex flex-wrap items-center justify-between gap-4 p-5">
        <div className="flex items-center gap-3">
          <Switch id="si-active" label="On sale" checked={isActive} onCheckedChange={setIsActive} disabled={unpriced > 0 && !isActive} />
          <div className="text-sm">
            <p className="font-medium">On sale</p>
            <p className="text-muted">{unpriced > 0 ? `Price ${unpriced === 1 ? "1 option" : `${unpriced} options`} first.` : isActive ? "Parents can order it in the shop." : "Hidden from parents and the till."}</p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button variant="secondary" onClick={() => router.push("/admin/store")}>
            Back
          </Button>
          <Button onClick={save} loading={busy} loadingText="Saving…">
            {item ? "Save changes" : "Add item"}
          </Button>
        </div>
      </Card>
    </div>
  );
}
