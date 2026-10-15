"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { FeeItem, FeeSettings, GenerateInvoicesResult, InstalmentPlan, OptInStudent, PlanScope } from "@/lib/api/types";

/** Fees (E10 part 1). The API enforces Admin/Bursar (Finance policy) and admin-only settings. */

function done<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) {
    revalidatePath("/admin/fees", "layout");
    revalidatePath("/admin/invoices", "layout");
    revalidatePath("/admin/settings/fees");
  }
  return result;
}

export type FeeItemBody = { termId: string; name: string; amount: number; classLevelId: string | null; isMandatory: boolean; siblingDiscount: boolean };

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

export async function generateInvoices(termId: string, classLevelId: string | null): Promise<Loaded<GenerateInvoicesResult>> {
  return done(await load((api) => api.POST("/api/finance/invoices/generate", { body: { termId, classLevelId } })));
}
