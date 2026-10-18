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

/* ----------------------------------------------------------------- E11.5: the online form */

type FormBody = import("./form").ApplicationFormPayload;
type Detail = import("./form").ApplicationDetail;

export async function saveApplicationForm(id: string, body: FormBody): Promise<Loaded<Detail>> {
  return family(await load((api) => api.PUT("/api/admissions/applications/{id}/form", { params: { path: { id } }, body })));
}

/** 400 names the first missing item; after this the form and documents are locked. */
export async function submitApplication(id: string): Promise<Loaded<Detail>> {
  return family(await load((api) => api.POST("/api/admissions/applications/{id}/submit", { params: { path: { id } } })));
}

export async function removeApplicationDocument(id: string, documentId: string): Promise<Loaded<true>> {
  return family(
    await load(async (api) => {
      const r = await api.DELETE("/api/admissions/applications/{id}/documents/{documentId}", { params: { path: { id, documentId } } });
      return { ...r, data: r.response.ok ? (true as const) : undefined };
    }),
  );
}

/** Admin: correct a submitted form (audited). Documents can't be changed here. */
export async function correctApplicationForm(id: string, body: FormBody): Promise<Loaded<Detail>> {
  return staff(await load((api) => api.PUT("/api/admissions/applications/{id}/correction", { params: { path: { id } }, body })));
}
