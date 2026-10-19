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

/* ------------------------------------------------------------------ E16.2: tests and exams */

type TestDetail = import("./tests").TestDetail;
type TestInput = import("./tests").TestInput;
type SectionInput = import("./tests").SectionInput;
type QuestionKind = import("./questions").QuestionKind;

function tests<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) revalidatePath("/admin/tests", "layout");
  return result;
}

export async function createTest(body: TestInput): Promise<Loaded<TestDetail>> {
  return tests(await load((api) => api.POST("/api/cbt/tests", { body })));
}

/** Drafts only (422 test.locked once published). */
export async function updateTest(id: string, body: TestInput): Promise<Loaded<TestDetail>> {
  return tests(await load((api) => api.PUT("/api/cbt/tests/{id}", { params: { path: { id } }, body })));
}

/** Replaces every section, in order. */
export async function saveTestSections(id: string, sections: SectionInput[]): Promise<Loaded<TestDetail>> {
  return tests(await load((api) => api.PUT("/api/cbt/tests/{id}/sections", { params: { path: { id } }, body: { sections } })));
}

export async function publishTest(id: string): Promise<Loaded<TestDetail>> {
  return tests(await load((api) => api.POST("/api/cbt/tests/{id}/publish", { params: { path: { id } } })));
}

export async function unpublishTest(id: string): Promise<Loaded<TestDetail>> {
  return tests(await load((api) => api.POST("/api/cbt/tests/{id}/unpublish", { params: { path: { id } } })));
}

export async function deleteTest(id: string): Promise<Loaded<true>> {
  return tests(
    await load(async (api) => {
      const r = await api.DELETE("/api/cbt/tests/{id}", { params: { path: { id } } });
      return { ...r, data: r.response.ok ? (true as const) : undefined };
    }),
  );
}

/** The bank picker: one kind from one bank, a page at a time. */
export async function searchBank(q: { subjectId: string; classLevelId: string; kind: QuestionKind; search?: string; topic?: string; page: number }) {
  return load((api) => api.GET("/api/cbt/questions", { params: { query: { ...q, search: q.search || undefined, topic: q.topic || undefined, pageSize: 20 } } }));
}

export async function loadBankTopics(subjectId: string, classLevelId: string) {
  return load((api) => api.GET("/api/cbt/topics", { params: { query: { subjectId, classLevelId } } }));
}

/* ------------------------------------------------- E16.4–E16.5: marking, release, grade sheet */

type AttemptReview = import("./marking").AttemptReview;

/** Marks one answer (theory) or overrides the computer's mark. Final once results are released. */
export async function markAnswer(attemptId: string, itemId: string, score: number, comment: string | null): Promise<Loaded<AttemptReview>> {
  return tests(await load((api) => api.PUT("/api/cbt/attempts/{id}/items/{itemId}/mark", { params: { path: { id: attemptId, itemId } }, body: { score, comment } })));
}

/** 422 test.still_open / test.still_sitting / test.unmarked explain what's missing. */
export async function releaseResults(testId: string): Promise<Loaded<TestDetail>> {
  return tests(await load((api) => api.POST("/api/cbt/tests/{id}/release", { params: { path: { id: testId } } })));
}

/** Writes scaled scores for one component; never touches published reports. Safe to re-run. */
export async function pushToGradebook(testId: string, studentIds: string[] | null) {
  return tests(await load((api) => api.POST("/api/cbt/tests/{id}/gradebook", { params: { path: { id: testId } }, body: { studentIds } })));
}

/* ------------------------------------------------------------- E16.8–E16.9: AI questions */

type DraftInput = import("./ai").DraftInput;

function ai<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) revalidatePath("/admin/cbt", "layout");
  return result;
}

/** A fresh version that avoids the earlier ones (at most 5). Uses 1 from the allowance when it succeeds. */
export async function regenerateQuestions(id: string) {
  return ai(await load((api) => api.POST("/api/cbt/ai/generations/{id}/regenerate", { params: { path: { id } } })));
}

/** All or nothing; 400 errors are keyed questions[i].field. They go to the bank as source "Ai". */
export async function saveAiDrafts(id: string, questions: DraftInput[]) {
  return ai(await load((api) => api.POST("/api/cbt/ai/generations/{id}/save", { params: { path: { id } }, body: { questions } })));
}

export async function saveAiSettings(body: { enabled: boolean; defaultMonthlyGenerations: number; schoolMonthlyGenerations: number }) {
  const r = await load((api) => api.PUT("/api/cbt/ai/settings", { body }));
  if (r.ok) revalidatePath("/admin/settings/ai");
  return r;
}

/** null = back to the school default. */
export async function setAiAllowance(userId: string, monthlyGenerations: number | null) {
  const r = await load((api) => api.PUT("/api/cbt/ai/allowances/{userId}", { params: { path: { userId } }, body: { monthlyGenerations } }));
  if (r.ok) revalidatePath("/admin/settings/ai");
  return r;
}
