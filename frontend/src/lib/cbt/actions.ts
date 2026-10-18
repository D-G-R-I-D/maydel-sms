"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { Question, QuestionInput } from "./questions";

/** E16.1 question bank (Admin, Teacher). The API enforces who may write for which subject and level. */

function bank<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) revalidatePath("/admin/cbt", "layout");
  return result;
}

export async function createQuestion(body: QuestionInput): Promise<Loaded<Question>> {
  return bank(await load((api) => api.POST("/api/cbt/questions", { body })));
}

export async function updateQuestion(id: string, body: QuestionInput): Promise<Loaded<Question>> {
  return bank(await load((api) => api.PUT("/api/cbt/questions/{id}", { params: { path: { id } }, body })));
}

/** Archives: the question leaves the bank; tests that already use it keep it. */
export async function archiveQuestion(id: string): Promise<Loaded<true>> {
  return bank(
    await load(async (api) => {
      const r = await api.DELETE("/api/cbt/questions/{id}", { params: { path: { id } } });
      return { ...r, data: r.response.ok ? (true as const) : undefined };
    }),
  );
}

/** After a picture upload or removal through the BFF, so lists and the editor show it. */
export async function refreshBank(): Promise<void> {
  revalidatePath("/admin/cbt", "layout");
}
