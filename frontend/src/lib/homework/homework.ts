import type { components } from "@/lib/api/schema";
import { isBlankHtml } from "@/lib/website/posts";

/** E17.1 homework: teachers set it per class (and subject); students hand in; parents follow. */
export type Homework = components["schemas"]["HomeworkDto"];
export type HomeworkFile = components["schemas"]["HomeworkFileDto"];
export type HomeworkInput = components["schemas"]["HomeworkInput"];
export type MyHomework = components["schemas"]["MyHomework"];
export type Submission = components["schemas"]["SubmissionDto"];
export type RosterRow = components["schemas"]["RosterRow"];
export type HandInState = components["schemas"]["HandInState"];
export type GradableClass = components["schemas"]["GradableClass"];

export const HomeworkCodes = { forbidden: "homework.forbidden", hasSubmissions: "homework.has_submissions", tooManyFiles: "homework.too_many_files", studentsOnly: "homework.students_only", noSubmissions: "homework.no_submissions" } as const;

export const MAX_FILES = 5;
export const MAX_FILE_BYTES = 10 * 1024 * 1024;
/** What the server accepts (it checks the content, not the name): PDF, photos and Office documents. */
export const HOMEWORK_ACCEPT =
  ".pdf,.jpg,.jpeg,.png,.webp,.docx,.xlsx,.pptx,application/pdf,image/jpeg,image/png,image/webp,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,application/vnd.openxmlformats-officedocument.presentationml.presentation";

export function checkHomeworkFile(f: { name: string; size: number }): string | null {
  if (!/\.(pdf|jpe?g|png|webp|docx|xlsx|pptx)$/i.test(f.name)) return `${f.name}: use a PDF, a photo, or a Word, Excel or PowerPoint file.`;
  if (f.size > MAX_FILE_BYTES) return `${f.name} is over 10 MB.`;
  return null;
}

export const stateLabel: Record<HandInState, string> = { Missing: "Not handed in", Submitted: "Handed in", Seen: "Seen", Returned: "Returned" };
export const stateTone: Record<HandInState, "neutral" | "brand" | "success" | "warning"> = { Missing: "neutral", Submitted: "brand", Seen: "success", Returned: "warning" };

/** "1.2 MB" / "340 KB". */
export function fileSize(bytes: number): string {
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toLocaleString("en-NG", { maximumFractionDigits: 1 })} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

const dayFmt = new Intl.DateTimeFormat("en-NG", { weekday: "short", day: "numeric", month: "short", timeZone: "UTC" });
/** "Due Mon, 27 Oct" / "Due today" / "Due tomorrow" / "Overdue (Fri, 24 Oct)". */
export function dueLabel(dueOn: string, today: string): string {
  if (dueOn === today) return "Due today";
  const t = new Date(`${today}T00:00:00Z`);
  t.setUTCDate(t.getUTCDate() + 1);
  if (dueOn === t.toISOString().slice(0, 10)) return "Due tomorrow";
  const d = dayFmt.format(new Date(`${dueOn}T00:00:00Z`));
  return dueOn < today ? `Was due ${d}` : `Due ${d}`;
}

/** Classes this person can set homework for, with the subjects they teach there. */
export type ClassChoice = { classRoomId: string; classRoom: string; levelOrder: number; subjects: { id: string; name: string }[]; isFormClass: boolean };
export function classChoices(rows: readonly GradableClass[]): ClassChoice[] {
  const map = new Map<string, ClassChoice>();
  for (const r of rows) {
    const c = map.get(r.classRoomId) ?? { classRoomId: r.classRoomId, classRoom: r.classRoom, levelOrder: r.levelOrder, subjects: [], isFormClass: false };
    if (!c.subjects.some((s) => s.id === r.subjectId)) c.subjects.push({ id: r.subjectId, name: r.subject });
    c.isFormClass ||= r.isFormClass;
    map.set(r.classRoomId, c);
  }
  return [...map.values()].sort((a, b) => a.levelOrder - b.levelOrder || a.classRoom.localeCompare(b.classRoom)).map((c) => ({ ...c, subjects: c.subjects.sort((a, b) => a.name.localeCompare(b.name)) }));
}

export type HomeworkForm = { classRoomId: string; subjectId: string; title: string; instructionsHtml: string; dueOn: string; acceptsSubmissions: boolean };

export function homeworkToForm(h: Homework | null, defaults: { classRoomId?: string; subjectId?: string; dueOn: string }): HomeworkForm {
  return {
    classRoomId: h?.classRoomId ?? defaults.classRoomId ?? "",
    subjectId: h ? (h.subjectId ?? "") : (defaults.subjectId ?? ""),
    title: h?.title ?? "",
    instructionsHtml: h?.instructionsHtml ?? (h?.instructions ? `<p>${h.instructions.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/\n/g, "<br>")}</p>` : ""),
    dueOn: h?.dueOn ?? defaults.dueOn,
    acceptsSubmissions: h?.acceptsSubmissions ?? true,
  };
}

export function homeworkErrors(f: HomeworkForm, today: string, existing: boolean): Partial<Record<keyof HomeworkForm, string>> {
  const e: Partial<Record<keyof HomeworkForm, string>> = {};
  if (!f.classRoomId) e.classRoomId = "Choose a class.";
  if (!f.title.trim()) e.title = "Give it a title, e.g. Fractions worksheet.";
  else if (f.title.length > 200) e.title = "200 characters at most.";
  if (!/^\d{4}-\d{2}-\d{2}$/.test(f.dueOn)) e.dueOn = "Choose the due date.";
  else if (!existing && f.dueOn < today) e.dueOn = "The due date can't be in the past.";
  return e;
}

export function formToHomeworkInput(f: HomeworkForm): HomeworkInput {
  const html = isBlankHtml(f.instructionsHtml) ? null : f.instructionsHtml;
  return { classRoomId: f.classRoomId, subjectId: f.subjectId || null, title: f.title.trim(), instructions: null, instructionsHtml: html, dueOn: f.dueOn, acceptsSubmissions: f.acceptsSubmissions };
}
