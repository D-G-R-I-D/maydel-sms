"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { FeeAudience, FeeItem, FeeSettings, GenerateInvoicesResult, PublishInvoicesResult, InstalmentPlan, InvoiceDetail, OptInStudent, PaymentMethod, PlanScope, Proof, Receipt } from "@/lib/api/types";

/** Fees (E10 part 1). The API enforces Admin/Bursar (Finance policy) and admin-only settings. */

function done<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) {
    revalidatePath("/admin/fees", "layout");
    revalidatePath("/admin/invoices", "layout");
    revalidatePath("/admin/settings/fees");
    revalidatePath("/admin/debtors");
    revalidatePath("/admin/students", "layout"); // fee statements
    revalidatePath("/admin", "layout"); // "To verify" badge
  }
  return result;
}

export type FeeItemBody = {
  termId: string;
  name: string;
  amount: number;
  classLevelId: string | null;
  isMandatory: boolean;
  siblingDiscount: boolean;
  audience: FeeAudience;
  chargedByDefault: boolean;
};

export async function createFeeItem(body: FeeItemBody): Promise<Loaded<FeeItem>> {
  return done(await load((api) => api.POST("/api/finance/fee-items", { body })));
}

export async function updateFeeItem(id: string, body: FeeItemBody): Promise<Loaded<FeeItem>> {
  return done(await load((api) => api.PUT("/api/finance/fee-items/{id}", { params: { path: { id } }, body })));
}

export async function deleteFeeItem(id: string): Promise<Loaded<true>> {
  return done(
    await load(async (api) => {
      const r = await api.DELETE("/api/finance/fee-items/{id}", { params: { path: { id } } });
      return { ...r, data: r.response.ok ? (true as const) : undefined };
    }),
  );
}

export async function copyFeeItems(fromTermId: string, toTermId: string): Promise<Loaded<FeeItem[]>> {
  return done(await load((api) => api.POST("/api/finance/fee-items/copy", { body: { fromTermId, toTermId } })));
}

export async function loadOptIns(id: string): Promise<Loaded<OptInStudent[]>> {
  return load((api) => api.GET("/api/finance/fee-items/{id}/opt-ins", { params: { path: { id } } }));
}

export async function saveOptIns(id: string, studentIds: string[]): Promise<Loaded<OptInStudent[]>> {
  return done(await load((api) => api.PUT("/api/finance/fee-items/{id}/opt-ins", { params: { path: { id } }, body: { studentIds } })));
}

export async function saveFeeSettings(body: FeeSettings): Promise<Loaded<FeeSettings>> {
  return done(await load((api) => api.PUT("/api/finance/settings", { body })));
}

export async function saveInstalmentPlan(body: {
  termId: string;
  scope: PlanScope;
  enabled: boolean;
  instalments: { percent: number; dueOn: string }[];
  clearSession: boolean;
}): Promise<Loaded<InstalmentPlan>> {
  return done(await load((api) => api.PUT("/api/finance/instalment-plan", { body })));
}

/** E10.7: publish draft invoices (term, optional level) and by default email each guardian the invoice. */
export async function publishInvoices(termId: string, classLevelId: string | null, notifyParents: boolean): Promise<Loaded<PublishInvoicesResult>> {
  return done(await load((api) => api.POST("/api/finance/invoices/publish", { body: { termId, classLevelId, notifyParents } })));
}

export async function generateInvoices(termId: string, classLevelId: string | null): Promise<Loaded<GenerateInvoicesResult>> {
  return done(await load((api) => api.POST("/api/finance/invoices/generate", { body: { termId, classLevelId } })));
}

/* ----------------------------------------------------------------- part 2a: payments */

export async function recordPayment(invoiceId: string, body: { amount: number; method: Exclude<PaymentMethod, "Online">; reference: string | null; paidOn: string | null }): Promise<Loaded<Receipt>> {
  return done(await load((api) => api.POST("/api/finance/invoices/{id}/payments", { params: { path: { id: invoiceId } }, body })));
}

/** Admin: a reversal is a new negative receipt; the original is never edited. */
export async function reversePayment(paymentId: string, reason: string): Promise<Loaded<Receipt>> {
  return done(await load((api) => api.POST("/api/finance/payments/{id}/reverse", { params: { path: { id: paymentId } }, body: { reason } })));
}

/** Admin: only when no money is on the invoice (422 invoice.has_payments). */
export async function cancelInvoice(invoiceId: string, reason: string): Promise<Loaded<InvoiceDetail>> {
  return done(await load((api) => api.POST("/api/finance/invoices/{id}/cancel", { params: { path: { id: invoiceId } }, body: { reason } })));
}

/** Admin: negative = scholarship/discount, positive = extra charge. */
export async function addAdjustment(invoiceId: string, description: string, amount: number): Promise<Loaded<InvoiceDetail>> {
  return done(await load((api) => api.POST("/api/finance/invoices/{id}/adjustments", { params: { path: { id: invoiceId } }, body: { description, amount } })));
}

/* ----------------------------------------------------------------- E10.8: proofs of payment */

/** Records the payment (RCT receipt, emailed to the parent). The instalment minimum is waived; the balance cap isn't. */
export async function confirmProof(id: string, body: { amount: number | null; method: "BankTransfer" | "Cash" | "Pos"; paidOn: string | null }): Promise<Loaded<Proof>> {
  return done(await load((api) => api.POST("/api/finance/proofs/{id}/confirm", { params: { path: { id } }, body })));
}

/** The reason is emailed to the parent. */
export async function rejectProof(id: string, reason: string): Promise<Loaded<Proof>> {
  return done(await load((api) => api.POST("/api/finance/proofs/{id}/reject", { params: { path: { id } }, body: { reason } })));
}

/** E12.8: overdue-fee reminders, sent in office hours and then every `intervalDays` until paid (admin). */
export async function saveFeeReminders(body: { enabled: boolean; intervalDays: number }): Promise<Loaded<{ enabled: boolean; intervalDays: number }>> {
  const r = await load((api) => api.PUT("/api/finance/reminders", { body }));
  if (r.ok) revalidatePath("/admin/settings/fees");
  return r;
}
