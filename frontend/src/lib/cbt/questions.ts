import { z } from "zod";
import type { components } from "@/lib/api/schema";

/** E16.1 question bank: kinds, labels and the editor's rules (mirroring Domain/Cbt/Questions.cs). */

type Schemas = components["schemas"];
export type Question = Schemas["QuestionDto"];
export type QuestionInput = Schemas["QuestionInput"];
export type QuestionKind = Schemas["QuestionKind"];
export type Difficulty = Schemas["Difficulty"];
export type AuthoringScope = Schemas["AuthoringScope"];

export const MAX_TEXT = 4000;
export const MAX_OPTIONS = 8;
export const MAX_OPTION_TEXT = 1000;
export const MAX_ANSWERS = 10;
export const MAX_ANSWER_TEXT = 200;
export const MAX_TOPIC = 80;

export const kindLabel: Record<QuestionKind, string> = {
  Objective: "Objective",
  MultiSelect: "Multi-select",
  TrueFalse: "True or false",
  FillInTheGap: "Fill in the gap",
  ShortAnswer: "Short answer",
  Theory: "Theory",
};
export const kindHint: Record<QuestionKind, string> = {
  Objective: "Options with exactly one correct answer.",
  MultiSelect: "Options with one or more correct answers. Full marks only for exactly the right set.",
  TrueFalse: "A statement the student marks true or false.",
  FillInTheGap: "A sentence with one gap, marked against the accepted answers.",
  ShortAnswer: "A short typed answer. Auto-marked when you give accepted answers; otherwise you mark it.",
  Theory: "A written answer you mark using the marking guide.",
};
export const KINDS = Object.keys(kindLabel) as QuestionKind[];
export const DIFFICULTIES: Difficulty[] = ["Easy", "Medium", "Hard"];
export const difficultyTone: Record<Difficulty, "success" | "warning" | "danger"> = { Easy: "success", Medium: "warning", Hard: "danger" };

/** The gap in a fill-in-the-gap sentence: three or more underscores. */
const GAP = /_{3,}/g;
export const gapCount = (text: string) => text.match(GAP)?.length ?? 0;
/** The sentence split around its gap, for the student preview; null unless there's exactly one. */
export function splitGap(text: string): [string, string] | null {
  if (gapCount(text) !== 1) return null;
  const [before, after] = text.split(/_{3,}/);
  return [before, after];
}

export const hasOptions = (k: QuestionKind) => k === "Objective" || k === "MultiSelect";
export const hasAnswers = (k: QuestionKind) => k === "FillInTheGap" || k === "ShortAnswer";
export const hasGuide = (k: QuestionKind) => k === "ShortAnswer" || k === "Theory";

/** The first line of the text, shortened for list rows. */
export function firstLine(text: string, max = 140): string {
  const line = text.trim().split(/\r?\n/)[0] ?? "";
  return line.length > max ? `${line.slice(0, max - 1).trimEnd()}…` : line;
}

const option = z.object({ id: z.string().nullable(), text: z.string(), isCorrect: z.boolean() });

export const questionSchema = z
  .object({
    kind: z.enum(KINDS as [QuestionKind, ...QuestionKind[]]),
    text: z.string().trim().min(1, "Write the question.").max(MAX_TEXT, `Keep the question under ${MAX_TEXT} characters.`),
    options: z.array(option),
    trueFalseAnswer: z.enum(["", "true", "false"]),
    acceptedAnswers: z.array(z.string()),
    markingGuide: z.string().trim().max(MAX_TEXT, `Keep the marking guide under ${MAX_TEXT} characters.`),
    topic: z.string().trim().max(MAX_TOPIC, `Keep the topic under ${MAX_TOPIC} characters.`),
    difficulty: z.enum(["Easy", "Medium", "Hard"]),
  })
  .superRefine((v, ctx) => {
    const issue = (path: string, message: string) => ctx.addIssue({ code: "custom", path: [path], message });
    if (hasOptions(v.kind)) {
      const texts = v.options.map((o) => o.text.trim());
      if (v.options.length < 2 || v.options.length > MAX_OPTIONS) issue("options", `Give between 2 and ${MAX_OPTIONS} options.`);
      else if (texts.some((t) => t.length === 0)) issue("options", "Every option needs text.");
      else if (texts.some((t) => t.length > MAX_OPTION_TEXT)) issue("options", `Keep each option under ${MAX_OPTION_TEXT} characters.`);
      else if (new Set(texts.map((t) => t.toLowerCase())).size !== texts.length) issue("options", "Two options are the same.");
      else {
        const correct = v.options.filter((o) => o.isCorrect).length;
        if (v.kind === "Objective" && correct !== 1) issue("options", "Mark exactly one option as correct.");
        if (v.kind === "MultiSelect" && correct < 1) issue("options", "Mark at least one option as correct.");
      }
    }
    if (v.kind === "TrueFalse" && v.trueFalseAnswer === "") issue("trueFalseAnswer", "Choose whether the statement is true or false.");
    if (v.kind === "FillInTheGap") {
      if (gapCount(v.text) !== 1) issue("text", "Mark the one gap in the sentence with three underscores: ___");
      if (v.acceptedAnswers.length === 0) issue("acceptedAnswers", "Give at least one accepted answer for the gap.");
    }
    if (hasAnswers(v.kind) && v.acceptedAnswers.length > MAX_ANSWERS) issue("acceptedAnswers", `Give up to ${MAX_ANSWERS} accepted answers.`);
    if (v.kind === "Theory" && v.markingGuide.trim() === "") issue("markingGuide", "Write the marking guide for this question.");
  });

export type QuestionFormValues = z.input<typeof questionSchema>;

export const blankOption = () => ({ id: null, text: "", isCorrect: false });

export function emptyValues(): QuestionFormValues {
  return { kind: "Objective", text: "", options: [blankOption(), blankOption(), blankOption(), blankOption()], trueFalseAnswer: "", acceptedAnswers: [], markingGuide: "", topic: "", difficulty: "Medium" };
}

export function questionValues(q: Question): QuestionFormValues {
  return {
    kind: q.kind,
    text: q.text,
    options: q.options.length > 0 ? q.options.map((o) => ({ id: o.id, text: o.text, isCorrect: o.isCorrect })) : [blankOption(), blankOption()],
    trueFalseAnswer: q.trueFalseAnswer === null ? "" : q.trueFalseAnswer ? "true" : "false",
    acceptedAnswers: q.acceptedAnswers,
    markingGuide: q.markingGuide ?? "",
    topic: q.topic ?? "",
    difficulty: q.difficulty,
  };
}

/** Form values → the API body, sending only what the kind uses (option ids kept so answers still match). */
export function toInput(v: QuestionFormValues, scope: { subjectId: string; classLevelId: string }): QuestionInput {
  return {
    subjectId: scope.subjectId,
    classLevelId: scope.classLevelId,
    kind: v.kind,
    text: v.text.trim(),
    options: hasOptions(v.kind) ? v.options.map((o) => ({ id: o.id, text: o.text.trim(), isCorrect: o.isCorrect })) : null,
    trueFalseAnswer: v.kind === "TrueFalse" ? v.trueFalseAnswer === "true" : null,
    acceptedAnswers: hasAnswers(v.kind) ? v.acceptedAnswers : null,
    markingGuide: hasGuide(v.kind) && v.markingGuide.trim() !== "" ? v.markingGuide.trim() : null,
    topic: v.topic.trim() === "" ? null : v.topic.trim(),
    difficulty: v.difficulty,
  };
}

/** Adds a typed answer to the chip list: trimmed, ≤ 200 characters, no case-insensitive duplicates. */
export function addAnswer(list: string[], raw: string): { list: string[]; error: string | null } {
  const a = raw.trim();
  if (a === "") return { list, error: null };
  if (a.length > MAX_ANSWER_TEXT) return { list, error: `Keep each answer under ${MAX_ANSWER_TEXT} characters.` };
  if (list.some((x) => x.toLowerCase() === a.toLowerCase())) return { list, error: "That answer is already in the list." };
  if (list.length >= MAX_ANSWERS) return { list, error: `Give up to ${MAX_ANSWERS} accepted answers.` };
  return { list: [...list, a], error: null };
}

/** Question pictures: the API re-encodes to PNG; mirror its accepted inputs. */
export const IMAGE_ACCEPT = "image/jpeg,image/png,image/webp";
export const MAX_IMAGE_BYTES = 12 * 1024 * 1024;
export function imageFileError(file: { type: string; size: number; name: string }): string | null {
  if (/\.(heic|heif)$/i.test(file.name) || /heic|heif/i.test(file.type)) return "iPhone HEIC photos aren't accepted. Share it as JPEG and try again.";
  if (!IMAGE_ACCEPT.split(",").includes(file.type)) return "Upload a JPEG, PNG or WebP picture.";
  if (file.size === 0) return "That file is empty.";
  if (file.size > MAX_IMAGE_BYTES) return "The picture must be 12 MB or smaller.";
  return null;
}

export const bankHref = (subjectId: string, classLevelId: string) => `/admin/cbt/bank?${new URLSearchParams({ subjectId, classLevelId }).toString()}`;
export const questionImageUrl = (id: string, version: string) => `/api/media/cbt/questions/${id}/image?v=${encodeURIComponent(version)}`;
