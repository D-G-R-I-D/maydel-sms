"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { Answer, AttemptPaper } from "./attempt";

/** E16.3: a student sitting a test. Student accounts only; the API checks the class and the window. */

/** Starts, or resumes with a fresh session token (any other open copy then gets 409 attempt.other_session). */
export async function startAttempt(testId: string): Promise<Loaded<AttemptPaper>> {
  const r = await load((api) => api.POST("/api/student/tests/{id}/start", { params: { path: { id: testId } } }));
  if (r.ok) revalidatePath("/portal/tests");
  return r;
}

export async function saveAnswer(attemptId: string, itemId: string, sessionToken: string, answer: Answer) {
  return load((api) => api.PUT("/api/student/attempts/{id}/items/{itemId}", { params: { path: { id: attemptId, itemId } }, body: { sessionToken, ...answer } }));
}

/** The focus log teachers see: Blur when the tab is hidden or loses focus, Focus when it's back. */
export async function logFocus(attemptId: string, sessionToken: string, kind: "Blur" | "Focus"): Promise<Loaded<true>> {
  return load(async (api) => {
    const r = await api.POST("/api/student/attempts/{id}/events", { params: { path: { id: attemptId } }, body: { sessionToken, kind } });
    return { ...r, data: r.response.ok ? (true as const) : undefined };
  });
}

export async function submitAttempt(attemptId: string, sessionToken: string): Promise<Loaded<AttemptPaper>> {
  const r = await load((api) => api.POST("/api/student/attempts/{id}/submit", { params: { path: { id: attemptId } }, body: { sessionToken } }));
  if (r.ok) revalidatePath("/portal/tests");
  return r;
}
