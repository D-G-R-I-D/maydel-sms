"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { SchoolProfile } from "@/lib/api/types";
import type { SchoolProfilePayload } from "@/lib/validation/school";

/** E2.6: the school's details. Images go through the /api/media/school BFF route instead. */
export async function updateSchoolProfile(body: SchoolProfilePayload): Promise<Loaded<SchoolProfile>> {
  const result = await load((api) => api.PUT("/api/school/profile", { body }));
  if (result.ok) revalidatePath("/admin/settings/school");
  return result;
}
