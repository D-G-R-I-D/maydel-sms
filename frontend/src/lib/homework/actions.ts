"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { Homework, HomeworkInput, Submission } from "./homework";

type Api = Parameters<Parameters<typeof load>[0]>[0];

function done(call: (api: Api) => Promise<{ response: Response; error?: unknown }>) {
  return load(async (api) => {
    const r = await call(api);
    return { ...r, data: r.response.ok ? (true as const) : undefined };
  });
}

function homework<T>(r: Loaded<T>): Loaded<T> {
  if (r.ok) revalidatePath("/admin/homework", "layout");
  return r;
}

/** E17.1 staff: set or change homework for a class (and subject). */
export async function saveHomework(id: string | null, body: HomeworkInput): Promise<Loaded<Homework>> {
  return homework(await load((api) => (id ? api.PUT("/api/homework/{id}", { params: { path: { id } }, body }) : api.POST("/api/homework", { body }))));
}

/** Only while nobody has handed in (422 homework.has_submissions). */
export async function deleteHomework(id: string): Promise<Loaded<true>> {
  return homework(await done((api) => api.DELETE("/api/homework/{id}", { params: { path: { id } } })));
}

export async function removeHomeworkFile(id: string, fileId: string): Promise<Loaded<Homework>> {
  return homework(await load((api) => api.DELETE("/api/homework/{id}/files/{fileId}", { params: { path: { id, fileId } } })));
}

/** Mark a hand-in Seen, or Return it with feedback (the student can hand in again). */
export async function reviewHandIn(id: string, submissionId: string, status: "Seen" | "Returned", feedback: string | null): Promise<Loaded<Submission>> {
  return homework(await load((api) => api.PUT("/api/homework/{id}/hand-ins/{submissionId}", { params: { path: { id, submissionId } }, body: { status, feedback } })));
}
