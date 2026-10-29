"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { components } from "@/lib/api/schema";
import type { Movement, PayMethod, StoreItem, StoreItemInput, StoreOrder, StoreSettings } from "./store";

type Variant = components["schemas"]["VariantDto"];

function store<T>(r: Loaded<T>): Loaded<T> {
  if (r.ok) revalidatePath("/admin/store", "layout");
  return r;
}

/** E19 admin/bursar: create or update an item with its options (options left out are retired). */
export async function saveStoreItem(id: string | null, body: StoreItemInput): Promise<Loaded<StoreItem>> {
  return store(await load((api) => (id ? api.PUT("/api/store/items/{id}", { params: { path: { id } }, body }) : api.POST("/api/store/items", { body }))));
}

export async function restockVariant(id: string, quantity: number, unitCost: number | null, supplier: string | null, note: string | null): Promise<Loaded<Variant>> {
  return store(await load((api) => api.POST("/api/store/variants/{id}/restock", { params: { path: { id } }, body: { quantity, unitCost, supplier, note } })));
}

export async function adjustVariant(id: string, change: number, reason: string): Promise<Loaded<Variant>> {
  return store(await load((api) => api.POST("/api/store/variants/{id}/adjust", { params: { path: { id } }, body: { change, reason } })));
}

export async function variantMovements(id: string): Promise<Loaded<Movement[]>> {
  return load((api) => api.GET("/api/store/variants/{id}/movements", { params: { path: { id } } }));
}

/** Office sale: handed over at once by default; "Fees" adds it to the student's current-term invoice. */
export async function sellInStore(studentId: string, lines: { variantId: string; quantity: number }[], payment: PayMethod, reference: string | null, handedOver: boolean): Promise<Loaded<StoreOrder>> {
  return store(await load((api) => api.POST("/api/store/orders", { body: { studentId, lines, payment, reference, handedOver } })));
}

export async function payOrder(id: string, method: PayMethod, reference: string | null): Promise<Loaded<StoreOrder>> {
  return store(await load((api) => api.POST("/api/store/orders/{id}/pay", { params: { path: { id } }, body: { method, reference } })));
}

export async function markOrderReady(id: string): Promise<Loaded<StoreOrder>> {
  return store(await load((api) => api.POST("/api/store/orders/{id}/ready", { params: { path: { id } } })));
}

export async function markOrderCollected(id: string): Promise<Loaded<StoreOrder>> {
  return store(await load((api) => api.POST("/api/store/orders/{id}/collected", { params: { path: { id } } })));
}

export async function cancelStoreOrder(id: string, reason: string | null): Promise<Loaded<StoreOrder>> {
  return store(await load((api) => api.POST("/api/store/orders/{id}/cancel", { params: { path: { id } }, body: { reason } })));
}

export async function saveStoreSettings(body: StoreSettings): Promise<Loaded<StoreSettings>> {
  return store(await load((api) => api.PUT("/api/store/settings", { body })));
}

/** Student search for an office sale (name or admission number). */
export async function searchStudentsForSale(q: string): Promise<Loaded<{ id: string; name: string; admissionNumber: string; classRoom: string | null }[]>> {
  const term = q.trim();
  if (term.length < 2) return { ok: true, data: [] };
  const r = await load((api) => api.GET("/api/students", { params: { query: { search: term, status: "Active", page: 1, pageSize: 8 } } }));
  return r.ok ? { ok: true, data: r.data.items.map((s) => ({ id: s.id, name: s.fullName, admissionNumber: s.admissionNumber, classRoom: s.classRoom })) } : r;
}

/** Parent shop: order for a child; held for the school's hold time, paid at the office or added to fees. */
export async function placeShopOrder(studentId: string, lines: { variantId: string; quantity: number }[], payment: "AtOffice" | "Fees"): Promise<Loaded<StoreOrder>> {
  const r = await load((api) => api.POST("/api/portal/children/{studentId}/shop/orders", { params: { path: { studentId } }, body: { lines, payment } }));
  if (r.ok) revalidatePath("/portal/shop", "layout");
  return r;
}

export async function cancelShopOrder(studentId: string, orderId: string): Promise<Loaded<StoreOrder>> {
  const r = await load((api) => api.POST("/api/portal/children/{studentId}/shop/orders/{orderId}/cancel", { params: { path: { studentId, orderId } } }));
  if (r.ok) revalidatePath("/portal/shop", "layout");
  return r;
}
