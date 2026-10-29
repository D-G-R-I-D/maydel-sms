import type { components } from "@/lib/api/schema";

/** E19 school store: uniforms, books and ID cards with options, stock, office sales and parent orders. */
export type StoreItem = components["schemas"]["StoreItemDto"];
export type Variant = components["schemas"]["VariantDto"];
export type StoreItemInput = components["schemas"]["StoreItemInput"];
export type StoreOrder = components["schemas"]["StoreOrderDto"];
export type Movement = components["schemas"]["MovementDto"];
export type LowStockRow = components["schemas"]["LowStockRow"];
export type StoreSettings = components["schemas"]["StoreSettingsDto"];
export type ShopCatalogue = components["schemas"]["ShopCatalogue"];
export type StoreCategory = components["schemas"]["StoreCategory"];
export type OrderStatus = components["schemas"]["OrderStatus"];
export type OrderPayment = components["schemas"]["OrderPayment"];
export type PayMethod = NonNullable<components["schemas"]["StorePayMethod"]>;

export const StoreCodes = { outOfStock: "store.out_of_stock", noInvoice: "store.no_invoice", negativeStock: "store.negative_stock", unpaid: "order.unpaid", state: "order.state", paidCancel: "order.paid_cancel", noFees: "shop.no_fees" } as const;

export const categories: { value: StoreCategory; label: string }[] = [
  { value: "Uniform", label: "Uniform" },
  { value: "Sportswear", label: "Sportswear" },
  { value: "Books", label: "Books" },
  { value: "IdCard", label: "ID cards" },
  { value: "Other", label: "Other" },
];
export const categoryLabel = (c: StoreCategory) => categories.find((x) => x.value === c)?.label ?? c;

export const storeSections = [
  { value: "Creche", label: "Creche" },
  { value: "Nursery", label: "Nursery" },
  { value: "Primary", label: "Primary" },
  { value: "JuniorSecondary", label: "Junior Sec." },
  { value: "SeniorSecondary", label: "Senior Sec." },
] as const;
export type StoreSection = (typeof storeSections)[number]["value"];

/** "Primary, JuniorSecondary" ↔ flags; empty/None = everyone. */
export function parseSections(s: string | null | undefined): StoreSection[] {
  const parts = (s ?? "").split(",").map((p) => p.trim());
  return storeSections.map((x) => x.value).filter((v) => parts.includes(v) || parts.includes("All"));
}
export function formatSections(list: readonly StoreSection[]): string {
  return list.length === 0 ? "None" : storeSections.map((x) => x.value).filter((v) => list.includes(v)).join(", ");
}
export function sectionsLabel(s: string): string {
  const list = parseSections(s);
  return list.length === 0 || list.length === storeSections.length ? "Everyone" : list.map((v) => storeSections.find((x) => x.value === v)!.label).join(", ");
}

export const officeMethods: { value: PayMethod; label: string }[] = [
  { value: "Cash", label: "Cash" },
  { value: "BankTransfer", label: "Bank transfer" },
  { value: "Pos", label: "POS" },
  { value: "Fees", label: "Add to fees" },
];
export const payMethodLabel = (m: PayMethod | null | undefined) =>
  ({ Cash: "Cash", BankTransfer: "Bank transfer", Pos: "POS", Fees: "Added to fees", AtOffice: "Pay at the office" })[m ?? "AtOffice"] ?? "—";

export const statusTone: Record<OrderStatus, "brand" | "warning" | "success" | "neutral"> = { Placed: "brand", Ready: "warning", Collected: "success", Cancelled: "neutral" };
export const statusLabel: Record<OrderStatus, string> = { Placed: "New", Ready: "Ready to collect", Collected: "Collected", Cancelled: "Cancelled" };
export const paymentLabel: Record<OrderPayment, string> = { Unpaid: "Unpaid", Paid: "Paid", ChargedToFees: "On fees" };

/** An item is on sale only when every active option has a price (the server's rule). */
export function unpricedOptions(variants: readonly { isActive: boolean; price: number }[]): number {
  return variants.filter((v) => v.isActive && !(v.price > 0)).length;
}

/** "From ₦4,500" style lowest active price, or null. */
export function lowestPrice(item: StoreItem): number | null {
  const prices = item.variants.filter((v) => v.isActive && v.price > 0).map((v) => v.price);
  return prices.length ? Math.min(...prices) : null;
}

export type CartLine = { variantId: string; item: string; variant: string; price: number; quantity: number; stock: number };
export const cartTotal = (lines: readonly CartLine[]) => lines.reduce((t, l) => t + l.price * l.quantity, 0);

/** Add or bump a line, capped at what's in stock. */
export function addToCart(lines: readonly CartLine[], add: CartLine): CartLine[] {
  const i = lines.findIndex((l) => l.variantId === add.variantId);
  if (i < 0) return [...lines, { ...add, quantity: Math.min(add.quantity, add.stock) }];
  return lines.map((l, j) => (j === i ? { ...l, quantity: Math.min(l.quantity + add.quantity, l.stock) } : l));
}
