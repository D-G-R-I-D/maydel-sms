import { z } from "zod";
import type { SchoolSection, TermNumber } from "@/lib/api/types";

/** Client mirrors of the M1.1 rules (docs/handoffs/m1.1-school-setup.md). The API re-checks everything. */

const isoDate = z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "Pick a date.");
const optionalDate = z.union([isoDate, z.literal("")]);

export const TERM_NUMBERS = ["First", "Second", "Third"] as const satisfies readonly TermNumber[];

export const sessionSchema = z
  .object({
    name: z
      .string()
      .trim()
      .regex(/^\d{4}\/\d{4}$/, "Use the form 2027/2028.")
      .refine((v) => {
        const [a, b] = v.split("/").map(Number);
        return b === a + 1;
      }, "The two years must be consecutive, e.g. 2027/2028."),
    startsOn: isoDate,
    endsOn: isoDate,
  })
  .refine((v) => v.endsOn > v.startsOn, { path: ["endsOn"], message: "The session must end after it starts." });
export type SessionInput = z.infer<typeof sessionSchema>;

/** Suggested name for the next session, from the newest one (or the current school year). */
export function nextSessionName(latest: string | undefined, today = new Date()): string {
  const start = latest && /^\d{4}\/\d{4}$/.test(latest) ? Number(latest.slice(0, 4)) + 1 : today.getMonth() >= 6 ? today.getFullYear() : today.getFullYear() - 1;
  return `${start}/${start + 1}`;
}

type TermBounds = { sessionStartsOn: string; sessionEndsOn: string };

export function termSchema(bounds: TermBounds) {
  return z
    .object({
      number: z.enum(TERM_NUMBERS),
      startsOn: isoDate,
      endsOn: isoDate,
      midTermStartsOn: optionalDate,
      midTermEndsOn: optionalDate,
    })
    .superRefine((v, ctx) => {
      if (v.endsOn <= v.startsOn) ctx.addIssue({ code: "custom", path: ["endsOn"], message: "The term must end after it starts." });
      if (v.startsOn < bounds.sessionStartsOn)
        ctx.addIssue({ code: "custom", path: ["startsOn"], message: `Must be on or after the session start (${bounds.sessionStartsOn}).` });
      if (v.endsOn > bounds.sessionEndsOn)
        ctx.addIssue({ code: "custom", path: ["endsOn"], message: `Must be on or before the session end (${bounds.sessionEndsOn}).` });
      const hasStart = v.midTermStartsOn !== "";
      const hasEnd = v.midTermEndsOn !== "";
      if (hasStart !== hasEnd) {
        ctx.addIssue({ code: "custom", path: [hasStart ? "midTermEndsOn" : "midTermStartsOn"], message: "A mid-term break needs both dates." });
      } else if (hasStart && hasEnd) {
        if (v.midTermEndsOn < v.midTermStartsOn)
          ctx.addIssue({ code: "custom", path: ["midTermEndsOn"], message: "The break must end on or after it starts." });
        if (v.midTermStartsOn < v.startsOn || v.midTermEndsOn > v.endsOn)
          ctx.addIssue({ code: "custom", path: ["midTermStartsOn"], message: "The break must fall inside the term." });
      }
    });
}
export type TermInput = z.infer<ReturnType<typeof termSchema>>;

/** The API takes null, not "", for a missing mid-term break. */
export function termPayload(v: TermInput) {
  return {
    startsOn: v.startsOn,
    endsOn: v.endsOn,
    midTermStartsOn: v.midTermStartsOn || null,
    midTermEndsOn: v.midTermEndsOn || null,
  };
}

/** Terms already in a session: the number picker only offers the next one, so they stay in order. */
export function nextTermNumber(existing: readonly TermNumber[]): TermNumber | null {
  return TERM_NUMBERS.find((n) => !existing.includes(n)) ?? null;
}

export function classroomSchema(minCapacity = 1) {
  return z.object({
    classLevelId: z.string().min(1, "Choose a year group."),
    arm: z.string().trim().min(1, "Enter the arm, e.g. A.").max(32, "Use 32 characters or fewer."),
    capacity: z.coerce
      .number<unknown>({ error: "Enter a number." })
      .int("Use a whole number.")
      .min(Math.max(1, minCapacity), minCapacity > 1 ? `The class already has ${minCapacity} students.` : "At least 1.")
      .max(200, "At most 200."),
  });
}
export type ClassroomInput = z.infer<ReturnType<typeof classroomSchema>>;
export type ClassroomFormValues = z.input<ReturnType<typeof classroomSchema>>;

export const subjectSchema = z.object({
  name: z.string().trim().min(1, "Enter the subject name.").max(100, "Use 100 characters or fewer."),
  code: z
    .string()
    .trim()
    .min(1, "Enter a short code, e.g. MTH.")
    .max(10, "Use 10 characters or fewer.")
    .regex(/^[A-Za-z0-9]+$/, "Letters and digits only.")
    .transform((v) => v.toUpperCase()),
  isActive: z.boolean(),
});
export type SubjectInput = z.input<typeof subjectSchema>;

export const sectionLabels: Record<SchoolSection, string> = {
  Creche: "Creche",
  Nursery: "Nursery",
  Primary: "Primary",
  JuniorSecondary: "Junior Secondary",
  SeniorSecondary: "Senior Secondary",
};

export const SECTION_ORDER = ["Creche", "Nursery", "Primary", "JuniorSecondary", "SeniorSecondary"] as const satisfies readonly SchoolSection[];

/** Groups classes by school section, keeping the API's level order inside each group. */
export function groupBySection<T extends { section: SchoolSection; levelOrder: number; name: string }>(rows: readonly T[]) {
  return SECTION_ORDER.map((section) => ({
    section,
    label: sectionLabels[section],
    rows: rows.filter((r) => r.section === section).sort((a, b) => a.levelOrder - b.levelOrder || a.name.localeCompare(b.name)),
  })).filter((g) => g.rows.length > 0);
}

const dateFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" });

/** "2026-09-07" → "7 Sept 2026". Dates are calendar days, so format in UTC to avoid shifting. */
export function formatDay(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(`${iso}T00:00:00Z`);
  return Number.isNaN(d.getTime()) ? iso : dateFormat.format(d);
}

export function formatRange(from: string, to: string): string {
  return `${formatDay(from)} – ${formatDay(to)}`;
}
