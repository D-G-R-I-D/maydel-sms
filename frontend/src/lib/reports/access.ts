import "server-only";
import { load } from "@/lib/api/server";

/** Is the signed-in teacher the form teacher of this class? (UX only; the API enforces it.) */
export async function isFormTeacherOf(classRoomId: string): Promise<boolean> {
  const mine = await load((api) => api.GET("/api/assessment/my-classes"));
  return mine.ok && mine.data.some((c) => c.classRoomId === classRoomId && c.isFormClass);
}
