"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { PromotionDecision, PromotionSummary, PromotionUndone } from "./promotion";

function done<T>(r: Loaded<T>): Loaded<T> {
  if (r.ok) {
    revalidatePath("/admin/academics", "layout");
    revalidatePath("/admin/students", "layout");
  }
  return r;
}

/** E3.6: apply a batch of decisions in one transaction (capacity checked after every move). */
export async function applyPromotions(sessionId: string, decisions: PromotionDecision[]): Promise<Loaded<PromotionSummary>> {
  return done(await load((api) => api.POST("/api/promotions", { body: { sessionId, decisions } })));
}

/** E3.6: put one class's students back where they were; anyone changed by hand since is skipped. */
export async function undoPromotion(sessionId: string, classRoomId: string): Promise<Loaded<PromotionUndone>> {
  return done(await load((api) => api.POST("/api/promotions/undo", { body: { sessionId, classRoomId } })));
}
