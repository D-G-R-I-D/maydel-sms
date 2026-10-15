"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { AttendanceStatus, ClassRegister } from "@/lib/api/types";

/** E9.1: save a class's daily register. Students left out are recorded Present by the API. */
export async function saveRegister(body: {
  classRoomId: string;
  date: string;
  entries: { studentId: string; status: AttendanceStatus; note: string | null }[];
}): Promise<Loaded<ClassRegister>> {
  const result = await load((api) => api.PUT("/api/attendance/register", { body }));
  if (result.ok) {
    revalidatePath("/admin/attendance");
    revalidatePath("/admin/reports", "layout"); // register totals feed report attendance
  }
  return result;
}
