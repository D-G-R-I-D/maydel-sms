import "server-only";
import { load } from "@/lib/api/server";

/** Sessions for admissions pickers, and the one to show: `?sessionId=` if valid, else the current one, else the newest. */
export async function loadAdmissionSessions(requested: string | undefined) {
  const sessions = await load((api) => api.GET("/api/academics/sessions"));
  const list = sessions.ok ? sessions.data.map((s) => ({ id: s.id, name: s.name, isCurrent: s.isCurrent })) : [];
  const selected = list.find((s) => s.id === requested) ?? list.find((s) => s.isCurrent) ?? list[0] ?? null;
  return { sessions: list, selected };
}
