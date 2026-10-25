"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { components } from "@/lib/api/schema";
import type { Bus, CheckQuestion, Run, Trip } from "./transport";

type BusInput = components["schemas"]["BusInput"];
type RunInput = components["schemas"]["RunInput"];
type CheckQuestionInput = components["schemas"]["CheckQuestionInput"];

function transport<T>(r: Loaded<T>): Loaded<T> {
  if (r.ok) revalidatePath("/admin/transport", "layout");
  return r;
}

/** E18 admin: buses (name, plate, capacity, driver; retire with isActive). */
export async function saveBus(id: string | null, body: BusInput): Promise<Loaded<Bus>> {
  return transport(await load((api) => (id ? api.PUT("/api/transport/buses/{id}", { params: { path: { id } }, body }) : api.POST("/api/transport/buses", { body }))));
}

/** The whole list of runs: rename, reorder, add or retire (runs are never deleted). */
export async function saveRuns(body: RunInput[]): Promise<Loaded<Run[]>> {
  return transport(await load((api) => api.PUT("/api/transport/runs", { body })));
}

/** The whole checklist: questions, their kind, the "good" answer and whether any answer flags. */
export async function saveQuestions(body: CheckQuestionInput[]): Promise<Loaded<CheckQuestion[]>> {
  return transport(await load((api) => api.PUT("/api/transport/questions", { body })));
}

/** Set a run's times for a day (WAT instants); the reason is required and kept. */
export async function correctTrip(busId: string, runId: string, date: string, body: { leftAt: string | null; backAt: string | null; note: string }): Promise<Loaded<Trip>> {
  return transport(await load((api) => api.PUT("/api/transport/trips/{busId}/{runId}/{date}", { params: { path: { busId, runId, date } }, body })));
}
