"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { Check, CheckAnswerInput, Trip, TripEvent } from "./transport";

/** E18 driver (or an admin standing in): tap Left / Back for a run, undo a mis-tap, submit the checklist. */
export async function tapTrip(busId: string, runId: string, event: TripEvent): Promise<Loaded<Trip>> {
  const r = await load((api) => api.POST("/api/driver/trips", { body: { busId, runId, event, note: null } }));
  if (r.ok) revalidatePath("/driver");
  return r;
}

export async function undoTrip(busId: string, runId: string): Promise<Loaded<Trip>> {
  const r = await load((api) => api.POST("/api/driver/trips/undo", { body: { busId, runId } }));
  if (r.ok) revalidatePath("/driver");
  return r;
}

export async function submitCheck(busId: string, answers: CheckAnswerInput[], notes: string | null): Promise<Loaded<Check>> {
  const r = await load((api) => api.POST("/api/driver/checks", { body: { busId, answers, notes } }));
  if (r.ok) revalidatePath("/driver");
  return r;
}
