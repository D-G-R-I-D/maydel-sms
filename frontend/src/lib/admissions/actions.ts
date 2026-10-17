"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { AdmissionSettings, Application, FormCode, OnlineCheckout } from "@/lib/api/types";
import type { ChildPayload } from "./rules";

/** E11 admissions. The API enforces roles (family: Applicant/Parent; staff: Admin/Bursar). */

function family<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) revalidatePath("/admissions", "layout");
  return result;
}
function staff<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) {
    revalidatePath("/admin/admissions", "layout");
    revalidatePath("/admin/settings/admissions");
  }
  return result;
}

export async function applyForChild(body: ChildPayload): Promise<Loaded<Application>> {
  return family(await load((api) => api.POST("/api/admissions/applications", { body })));
}

export async function redeemFormCode(applicationId: string, code: string): Promise<Loaded<Application>> {
  return family(await load((api) => api.POST("/api/admissions/applications/{id}/redeem-code", { params: { path: { id: applicationId } }, body: { code } })));
}

/** Starts a Paystack checkout for the form fee; the browser then goes to `authorizationUrl`. */
export async function payForm(applicationId: string): Promise<Loaded<OnlineCheckout>> {
  return load((api) => api.POST("/api/admissions/applications/{id}/pay", { params: { path: { id: applicationId } } }));
}

export async function checkFormPayment(reference: string) {
  return load((api) => api.GET("/api/admissions/payments/{reference}", { params: { path: { reference } } }));
}

export async function saveAdmissionSettings(body: { sessionId: string; isOpen: boolean; formFee: number; examWindowDays: number; instructions: string | null }): Promise<Loaded<AdmissionSettings>> {
  return staff(await load((api) => api.PUT("/api/admissions/settings", { body })));
}

export async function issueFormCodes(body: { sessionId: string; count: number; amountPaid: number; issuedTo: string | null }): Promise<Loaded<FormCode[]>> {
  return staff(await load((api) => api.POST("/api/admissions/form-codes", { body })));
}
