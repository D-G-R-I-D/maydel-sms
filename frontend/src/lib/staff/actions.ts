"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { IssuedCredentials, Role, StaffCreated, StaffDetail } from "@/lib/api/types";

/**
 * Staff mutations (M1.1 part 2). Responses that carry a temporary password go straight back to
 * the caller's browser for the one-time credential slip; nothing here logs or stores them.
 */

type StaffBody = { fullName: string; email: string; phone: string | null; jobTitle: string | null; roles: Role[] };

function done<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) {
    revalidatePath("/admin/staff", "layout");
    revalidatePath("/admin/academics", "layout"); // teacher pickers and assignments
  }
  return result;
}

export async function createStaff(body: StaffBody): Promise<Loaded<StaffCreated>> {
  return done(await load((api) => api.POST("/api/staff", { body })));
}

export async function updateStaff(id: string, body: StaffBody & { isActive: boolean }): Promise<Loaded<StaffDetail>> {
  return done(await load((api) => api.PUT("/api/staff/{id}", { params: { path: { id } }, body })));
}

export async function resetStaffPassword(id: string): Promise<Loaded<IssuedCredentials>> {
  return done(await load((api) => api.POST("/api/staff/{id}/reset-password", { params: { path: { id } } })));
}
