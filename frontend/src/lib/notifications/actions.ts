"use server";

import { load, type Loaded } from "@/lib/api/server";
import type { Bell } from "./bell";

/**
 * E12.8 bell: the user's own notifications (payments, overdue fees; homework later) and the school's
 * notices for their audience, read together. Either feed failing leaves the other showing.
 */
export async function loadBell(): Promise<Bell> {
  const [notifications, notices] = await Promise.all([load((api) => api.GET("/api/notifications")), load((api) => api.GET("/api/notices"))]);
  return {
    notifications: notifications.ok ? notifications.data.items : [],
    notices: notices.ok ? notices.data.notices : [],
    unread: (notifications.ok ? notifications.data.unread : 0) + (notices.ok ? notices.data.unread : 0),
    failed: !notifications.ok && !notices.ok,
  };
}

type Api = Parameters<Parameters<typeof load>[0]>[0];

/** The read endpoints answer 204 No Content; count any 2xx as done. */
function done(call: (api: Api) => Promise<{ response: Response; error?: unknown }>) {
  return load(async (api) => {
    const r = await call(api);
    return { ...r, data: r.response.ok ? (true as const) : undefined };
  });
}

export async function markNotificationRead(id: string): Promise<Loaded<true>> {
  return done((api) => api.POST("/api/notifications/{id}/read", { params: { path: { id } } }));
}

export async function markNoticeRead(id: string): Promise<Loaded<true>> {
  return done((api) => api.POST("/api/notices/{id}/read", { params: { path: { id } } }));
}

/** Marks both feeds read. */
export async function markAllRead(): Promise<Loaded<true>> {
  const [a, b] = await Promise.all([done((api) => api.POST("/api/notifications/read-all")), done((api) => api.POST("/api/notices/read-all"))]);
  return a.ok ? b : a;
}
