import type { InvoiceStatus } from "@/lib/api/types";

/** Invoice list filters kept in the URL (E10). */
export const INVOICE_STATUSES: InvoiceStatus[] = ["Unpaid", "PartiallyPaid", "Paid", "Cancelled"];
export const INVOICE_PAGE_SIZE = 25;

export type InvoiceSearch = { termId?: string; classRoomId?: string; status?: InvoiceStatus; search?: string; published?: boolean; newIntake?: boolean; page: number };
type SP = Record<string, string | string[] | undefined>;
const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

export function parseInvoiceSearch(sp: SP): InvoiceSearch {
  const status = one(sp.status);
  const page = Number(one(sp.page));
  return {
    termId: one(sp.termId),
    classRoomId: one(sp.classRoomId),
    status: INVOICE_STATUSES.includes(status as InvoiceStatus) ? (status as InvoiceStatus) : undefined,
    search: one(sp.search)?.trim().slice(0, 100) || undefined,
    published: one(sp.published) === "true" ? true : one(sp.published) === "false" ? false : undefined,
    newIntake: one(sp.newIntake) === "true" ? true : undefined,
    page: Number.isInteger(page) && page > 0 ? page : 1,
  };
}

export function invoicesHref(search: InvoiceSearch, overrides: Partial<InvoiceSearch> = {}): string {
  const s = { ...search, ...overrides };
  const q = new URLSearchParams();
  if (s.termId) q.set("termId", s.termId);
  if (s.classRoomId) q.set("classRoomId", s.classRoomId);
  if (s.status) q.set("status", s.status);
  if (s.search) q.set("search", s.search);
  if (s.published !== undefined) q.set("published", String(s.published));
  if (s.newIntake) q.set("newIntake", "true");
  if (s.page > 1) q.set("page", String(s.page));
  const qs = q.toString();
  return qs ? `/admin/invoices?${qs}` : "/admin/invoices";
}
