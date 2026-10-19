import { z } from "zod";
import type { components } from "@/lib/api/schema";
import { lagosIso, toLagosParts } from "@/lib/time/lagos";
import type { Difficulty, QuestionKind } from "./questions";

/** E16.2: tests and exams built from sections (mirroring Domain/Cbt/Tests.cs). Times shown in Lagos. */

type Schemas = components["schemas"];
export type TestSummary = Schemas["TestSummary"];
export type TestDetail = Schemas["TestDetail"];
export type TestInput = Schemas["TestInput"];
export type SectionInput = Schemas["SectionInput"];
export type SectionDto = Schemas["SectionDto"];
export type QuestionBrief = Schemas["QuestionBrief"];
export type SectionMode = Schemas["SectionMode"];
export type TestStatus = Schemas["TestStatus"];
export type ComponentOption = { id: string; name: string; maxScore: number };

export const MAX_SECTIONS = 10;
export const MAX_TEST_QUESTIONS = 200;

const intIn = (min: number, max: number, message: string) =>
  z.string().trim().refine((v) => /^\d+$/.test(v) && Number(v) >= min && Number(v) <= max, message);

export const testHeaderSchema = z
  .object({
    title: z.string().trim().min(1, "Give the test a title.").max(150, "Keep the title under 150 characters."),
    instructions: z.string().trim().max(2000, "Keep the instructions under 2000 characters."),
    scope: z.string().min(1, "Choose the subject and class."),
    termId: z.string().min(1, "Choose the term."),
    componentId: z.string(),
    classRoomIds: z.array(z.string()).min(1, "Choose at least one class to sit the test.").max(30, "Choose up to 30 classes."),
    durationMinutes: intIn(5, 300, "The sitting must last between 5 and 300 minutes."),
    opensDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "Choose the opening date."),
    opensTime: z.string().regex(/^\d{2}:\d{2}$/, "Choose the opening time."),
    closesDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "Choose the closing date."),
    closesTime: z.string().regex(/^\d{2}:\d{2}$/, "Choose the closing time."),
    maxAttempts: intIn(1, 5, "Allow between 1 and 5 attempts."),
    shuffleQuestions: z.boolean(),
    shuffleOptions: z.boolean(),
    showScoreOnSubmit: z.boolean(),
  })
  .superRefine((v, ctx) => {
    const opens = Date.parse(lagosIso(v.opensDate, v.opensTime));
    const closes = Date.parse(lagosIso(v.closesDate, v.closesTime));
    if (Number.isNaN(opens) || Number.isNaN(closes)) return;
    if (closes <= opens) ctx.addIssue({ code: "custom", path: ["closesTime"], message: "The test must close after it opens." });
    else if ((closes - opens) / 60_000 < Number(v.durationMinutes))
      ctx.addIssue({ code: "custom", path: ["closesTime"], message: "The window is shorter than the sitting. Close it later or shorten the test." });
  });

export type TestHeaderValues = z.input<typeof testHeaderSchema>;
export const scopeKey = (subjectId: string, classLevelId: string) => `${subjectId}|${classLevelId}`;
export const parseScope = (key: string) => {
  const [subjectId = "", classLevelId = ""] = key.split("|");
  return { subjectId, classLevelId };
};

/** A new test: opens tomorrow 09:00 and closes 17:00 (Lagos), 40 minutes, one attempt, shuffled. */
export function newHeaderValues(termId: string, now: Date = new Date()): TestHeaderValues {
  const tomorrow = toLagosParts(new Date(now.getTime() + 24 * 3600 * 1000).toISOString()).date;
  return {
    title: "",
    instructions: "",
    scope: "",
    termId,
    componentId: "",
    classRoomIds: [],
    durationMinutes: "40",
    opensDate: tomorrow,
    opensTime: "09:00",
    closesDate: tomorrow,
    closesTime: "17:00",
    maxAttempts: "1",
    shuffleQuestions: true,
    shuffleOptions: true,
    showScoreOnSubmit: false,
  };
}

export function headerValues(t: TestDetail): TestHeaderValues {
  const opens = toLagosParts(t.opensAt);
  const closes = toLagosParts(t.closesAt);
  return {
    title: t.title,
    instructions: t.instructions ?? "",
    scope: scopeKey(t.subjectId, t.classLevelId),
    termId: t.termId,
    componentId: t.componentId ?? "",
    classRoomIds: t.classRooms.map((c) => c.id),
    durationMinutes: String(t.durationMinutes),
    opensDate: opens.date,
    opensTime: opens.time,
    closesDate: closes.date,
    closesTime: closes.time,
    maxAttempts: String(t.maxAttempts),
    shuffleQuestions: t.shuffleQuestions,
    shuffleOptions: t.shuffleOptions,
    showScoreOnSubmit: t.showScoreOnSubmit,
  };
}

export function toTestInput(v: TestHeaderValues): TestInput {
  return {
    title: v.title.trim(),
    instructions: v.instructions.trim() === "" ? null : v.instructions.trim(),
    ...parseScope(v.scope),
    termId: v.termId,
    componentId: v.componentId === "" ? null : v.componentId,
    classRoomIds: v.classRoomIds,
    durationMinutes: Number(v.durationMinutes),
    opensAt: lagosIso(v.opensDate, v.opensTime),
    closesAt: lagosIso(v.closesDate, v.closesTime),
    maxAttempts: Number(v.maxAttempts),
    shuffleQuestions: v.shuffleQuestions,
    shuffleOptions: v.shuffleOptions,
    showScoreOnSubmit: v.showScoreOnSubmit,
  };
}

/* ------------------------------------------------------------------------------- sections */

/** A section as edited on the page (numbers kept as typed). `key` is local, for React lists. */
export type SectionDraft = {
  key: string;
  title: string;
  instructions: string;
  kind: QuestionKind;
  marksPerQuestion: string;
  mode: SectionMode;
  questions: QuestionBrief[];
  randomCount: string;
  topic: string;
  difficulty: "" | Difficulty;
  /** From the server for saved random sections; null when unknown (unsaved changes). */
  availableInBank: number | null;
};

let keySeq = 0;
const nextKey = () => `s${++keySeq}`;

export function newSection(kind: QuestionKind = "Objective", index = 0): SectionDraft {
  return { key: nextKey(), title: `Section ${String.fromCharCode(65 + index)}`, instructions: "", kind, marksPerQuestion: "1", mode: "Fixed", questions: [], randomCount: "10", topic: "", difficulty: "", availableInBank: null };
}

export function sectionDrafts(t: TestDetail): SectionDraft[] {
  return [...t.sections]
    .sort((a, b) => a.order - b.order)
    .map((s) => ({
      key: nextKey(),
      title: s.title,
      instructions: s.instructions ?? "",
      kind: s.kind,
      marksPerQuestion: String(s.marksPerQuestion),
      mode: s.mode,
      questions: s.questions,
      randomCount: String(s.randomCount ?? 10),
      topic: s.topic ?? "",
      difficulty: s.difficulty ?? "",
      availableInBank: s.availableInBank,
    }));
}

export function toSectionsInput(drafts: SectionDraft[]): SectionInput[] {
  return drafts.map((s) => ({
    title: s.title.trim(),
    instructions: s.instructions.trim() === "" ? null : s.instructions.trim(),
    kind: s.kind,
    marksPerQuestion: Number(s.marksPerQuestion),
    mode: s.mode,
    questionIds: s.mode === "Fixed" ? s.questions.map((q) => q.id) : null,
    randomCount: s.mode === "Random" ? Number(s.randomCount) : null,
    topic: s.mode === "Random" && s.topic.trim() !== "" ? s.topic.trim() : null,
    difficulty: s.mode === "Random" && s.difficulty !== "" ? s.difficulty : null,
  }));
}

const marksOk = (v: string) => /^\d+(\.\d{1,2})?$/.test(v.trim()) && Number(v) > 0 && Number(v) <= 100;
const countOf = (s: SectionDraft) => (s.mode === "Fixed" ? s.questions.length : /^\d+$/.test(s.randomCount.trim()) ? Number(s.randomCount) : 0);

/** Shape errors that stop a save, per section key (the server also checks bank membership). */
export function sectionErrors(drafts: SectionDraft[]): Record<string, string> {
  const e: Record<string, string> = {};
  for (const s of drafts) {
    if (s.title.trim() === "" || s.title.trim().length > 100) e[s.key] = "Give the section a title (up to 100 characters).";
    else if (!marksOk(s.marksPerQuestion)) e[s.key] = "Marks per question must be more than 0 and at most 100 (up to 2 decimals).";
    else if (s.mode === "Random" && !(countOf(s) >= 1 && countOf(s) <= MAX_TEST_QUESTIONS)) e[s.key] = `Draw between 1 and ${MAX_TEST_QUESTIONS} questions.`;
    else if (s.instructions.trim().length > 1000) e[s.key] = "Keep the section instructions under 1000 characters.";
  }
  return e;
}

/** Live totals: each section's marks (questions × marks per question), the count and the total. */
export function totals(drafts: SectionDraft[]): { marks: number[]; questionCount: number; totalMarks: number } {
  const marks = drafts.map((s) => (marksOk(s.marksPerQuestion) ? round2(countOf(s) * Number(s.marksPerQuestion)) : 0));
  return { marks, questionCount: drafts.reduce((n, s) => n + countOf(s), 0), totalMarks: round2(marks.reduce((a, b) => a + b, 0)) };
}

const round2 = (n: number) => Math.round(n * 100) / 100;
export const formatMarks = (n: number) => (Number.isInteger(n) ? String(n) : n.toFixed(2).replace(/0$/, ""));

/** "100 marks → Exams /40 (×0.40)", or a practice note. */
export function scalingLine(totalMarks: number, component: ComponentOption | null): string {
  if (!component) return "Practice test: marks don't go to the gradebook.";
  if (totalMarks <= 0) return `Scaled to ${component.name} /${formatMarks(component.maxScore)} once it has marks.`;
  const factor = component.maxScore / totalMarks;
  return `${formatMarks(totalMarks)} ${totalMarks === 1 ? "mark" : "marks"} → ${component.name} /${formatMarks(component.maxScore)} (×${factor.toFixed(2)})`;
}

export const testsHref = (termId?: string) => (termId ? `/admin/tests?termId=${termId}` : "/admin/tests");
