"use server";

import { load, type Loaded } from "@/lib/api/server";
import type { OnlineCheckout, OnlinePaymentResult } from "@/lib/api/types";

/** E10.5: start a Paystack checkout for one of the signed-in parent's/student's invoices. */
export async function startOnlinePayment(studentId: string, invoiceId: string, amount: number | null): Promise<Loaded<OnlineCheckout>> {
  return load((api) => api.POST("/api/portal/children/{studentId}/invoices/{invoiceId}/pay", { params: { path: { studentId, invoiceId } }, body: { amount } }));
}

/** E10.5: the outcome of a checkout (the API confirms with Paystack if the webhook hasn't yet). */
export async function checkOnlinePayment(reference: string): Promise<Loaded<OnlinePaymentResult>> {
  return load((api) => api.GET("/api/portal/payments/{reference}", { params: { path: { reference } } }));
}
