import type { components } from "@/lib/api/schema";
import { formatLagos } from "@/lib/time/lagos";
import { blankOption, questionSchema, toInput, type QuestionFormValues } from "./questions";

/** E16.8–E16.9: AI question writing (drafts the teacher edits before they reach the bank) and quotas. */

type Schemas = components["schemas"];
export type AiQuota = Schemas["AiQuotaDto"];
export type AiGeneration = Schemas["AiGenerationDto"];
export type AiVersion = Schemas["AiVersionDto"];
export type AiSummary = Schemas["AiGenerationSummary"];
export type AiSettings = Schemas["AiSettingsDto"];
export type AiUsage = Schemas["AiUsage"];
export type AiUsageRow = Schemas["AiUsageRow"];
export type AssessmentType = Schemas["AssessmentType"];
export type GeneratedQuestion = Schemas["GeneratedQuestion"];
export type DraftInput = Schemas["DraftInput"];

export const ASSESSMENT_TYPES: { value: AssessmentType; label: string }[] = [
  { value: "Test", label: "Class test" },
  { value: "Cat", label: "Continuous assessment (CA)" },
  { value: "Exam", label: "Exam" },
];
export const assessmentLabel = Object.fromEntries(ASSESSMENT_TYPES.map((t) => [t.value, t.label])) as Record<AssessmentType, string>;

export const MAX_AI_QUESTIONS = 40;
export const MAX_AI_FILES = 5;
export const MAX_GUIDANCE = 4000;
export const MAX_REGENERATIONS = 5;
export const AI_FILE_ACCEPT = "application/pdf,image/jpeg,image/png,image/webp";

/** Why generating is blocked right now, or null when it's allowed. */
export function quotaBlocker(q: AiQuota): string | null {
  if (!q.available) return "AI question writing isn't set up yet. Ask the school's administrator.";
  if (!q.enabled) return "AI question writing is switched off for the school.";
  if (q.schoolCap > 0 && q.schoolUsed >= q.schoolCap) return `The school has used this month's AI limit. It resets ${formatLagos(q.resetsAt)}.`;
  if (q.remaining <= 0) return `You've used this month's allowance (${q.allowance}). It resets ${formatLagos(q.resetsAt)}.`;
  return null;
}

export const quotaLabel = (q: AiQuota) => `${q.remaining} of ${q.allowance} left this month`;

/** An even easy/medium/hard split of `count`, leaning to medium. */
export function balancedMix(count: number): { easy: number; medium: number; hard: number } {
  const n = Math.max(0, Math.floor(count));
  const easy = Math.floor(n / 3);
  const hard = Math.floor(n / 3);
  return { easy, medium: n - easy - hard, hard };
}

export type AiRequestInput = { count: number; easy: number; medium: number; hard: number; kinds: string[]; guidance: string; files: { name: string; type: string; size: number }[] };

/** Client checks mirroring the API (400 kinds/count/easy/files/guidance). Keyed by field. */
export function aiRequestErrors(v: AiRequestInput): Partial<Record<"kinds" | "count" | "easy" | "files" | "guidance", string>> {
  const e: Partial<Record<"kinds" | "count" | "easy" | "files" | "guidance", string>> = {};
  if (v.kinds.length === 0) e.kinds = "Choose at least one kind of question.";
  if (!Number.isInteger(v.count) || v.count < 1 || v.count > MAX_AI_QUESTIONS) e.count = `Ask for between 1 and ${MAX_AI_QUESTIONS} questions.`;
  else if ([v.easy, v.medium, v.hard].some((n) => !Number.isInteger(n) || n < 0) || v.easy + v.medium + v.hard !== v.count)
    e.easy = `Easy, medium and hard must add up to ${v.count}.`;
  if (v.guidance.length > MAX_GUIDANCE) e.guidance = `Keep the guidance under ${MAX_GUIDANCE} characters.`;
  if (v.files.length > MAX_AI_FILES) e.files = `Upload at most ${MAX_AI_FILES} files.`;
  else {
    const bad = v.files.map(aiFileError).find(Boolean);
    if (bad) e.files = bad;
  }
  if (v.files.length === 0 && v.guidance.trim() === "") e.guidance = "Upload your notes or scheme of work, or describe what the questions should cover.";
  return e;
}

export function aiFileError(f: { name: string; type: string; size: number }): string | null {
  if (/\.(heic|heif)$/i.test(f.name) || /heic|heif/i.test(f.type)) return `${f.name}: iPhone HEIC photos aren't accepted. Share it as JPEG.`;
  if (f.type === "application/pdf") return f.size > 5 * 1024 * 1024 ? `${f.name}: PDFs must be 5 MB or smaller.` : null;
  if (["image/jpeg", "image/png", "image/webp"].includes(f.type)) return f.size > 12 * 1024 * 1024 ? `${f.name}: photos must be 12 MB or smaller.` : null;
  return `${f.name}: upload a PDF or a JPEG, PNG or WebP photo.`;
}

/** A generated (or new) question as editable form values. */
export function draftValues(q: GeneratedQuestion | null): QuestionFormValues {
  if (!q) return { kind: "Objective", text: "", options: [blankOption(), blankOption(), blankOption(), blankOption()], trueFalseAnswer: "", acceptedAnswers: [], markingGuide: "", topic: "", difficulty: "Medium" };
  return {
    kind: q.kind,
    text: q.text,
    options: q.options.length > 0 ? q.options.map((o) => ({ id: null, text: o.text, isCorrect: o.isCorrect })) : [blankOption(), blankOption()],
    trueFalseAnswer: q.trueFalseAnswer === null ? "" : q.trueFalseAnswer ? "true" : "false",
    acceptedAnswers: q.acceptedAnswers,
    markingGuide: q.markingGuide ?? "",
    topic: q.topic ?? "",
    difficulty: q.difficulty,
  };
}

/** Form values → the save body (no scope; the generation knows it). */
export function toDraftInput(v: QuestionFormValues): DraftInput {
  // Accepted answers are edited one per line, so drop blank lines.
  const { subjectId: _s, classLevelId: _c, ...rest } = toInput({ ...v, acceptedAnswers: v.acceptedAnswers.map((x) => x.trim()).filter(Boolean) }, { subjectId: "", classLevelId: "" });
  void _s;
  void _c;
  return rest;
}

/** The first client-side problem with a draft, or null when it would pass the bank's rules. */
export function draftProblem(v: QuestionFormValues): string | null {
  const r = questionSchema.safeParse({ ...v, acceptedAnswers: v.acceptedAnswers.map((x) => x.trim()).filter(Boolean) });
  return r.success ? null : (r.error.issues[0]?.message ?? "Check this question.");
}
