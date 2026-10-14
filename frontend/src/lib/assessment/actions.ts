"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { AssessmentSettings, BehaviourSettings, ComponentInput, GradeBand, GradeSheet, GradeSheetEntry } from "@/lib/api/types";

/**
 * Score entry autosave. Sends only the changed rows; returns the refreshed sheet (server totals,
 * grades, lock state). No revalidatePath: the grid replaces its own rows from the response.
 */
export async function saveGradeSheet(body: { classRoomId: string; subjectId: string; termId: string; entries: GradeSheetEntry[] }): Promise<Loaded<GradeSheet>> {
  return load((api) => api.PUT("/api/assessment/gradesheet", { body }));
}

export async function saveAssessmentSettings(body: { components: ComponentInput[]; bands: GradeBand[] }): Promise<Loaded<AssessmentSettings>> {
  const result = await load((api) => api.PUT("/api/assessment/settings", { body }));
  if (result.ok) revalidatePath("/admin", "layout");
  return result;
}

export async function saveBehaviourSettings(traits: { id: string | null; name: string; isActive: boolean }[]): Promise<Loaded<BehaviourSettings>> {
  const result = await load((api) => api.PUT("/api/assessment/behaviour", { body: { traits } }));
  if (result.ok) revalidatePath("/admin", "layout");
  return result;
}
