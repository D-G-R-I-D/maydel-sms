import { z } from "zod";
import type { GuardianRelationship, Sex, StudentStatus } from "@/lib/api/types";

/** Client mirrors of the M1.1 part 3 rules (docs/handoffs/m1.1-students.md). The API re-checks. */

export const SEXES = ["Female", "Male"] as const satisfies readonly Sex[];
export const RELATIONSHIPS = ["Mother", "Father", "Guardian", "Other"] as const satisfies readonly GuardianRelationship[];
export const STUDENT_STATUSES = ["Active", "Suspended", "Withdrawn", "Graduated"] as const satisfies readonly StudentStatus[];

const blankToNull = (max: number, label: string) =>
  z
    .string()
    .trim()
    .max(max, `${label} is too long (max ${max}).`)
    .transform((v) => (v === "" ? null : v));

/** Today as YYYY-MM-DD in the school's timezone (Africa/Lagos). */
export function schoolToday(now = new Date()): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Africa/Lagos", year: "numeric", month: "2-digit", day: "2-digit" }).format(now);
}

function addYears(iso: string, years: number): string {
  const [y, m, d] = iso.split("-").map(Number);
  return `${String(y + years).padStart(4, "0")}-${String(m).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
}

export function studentSchema(today = schoolToday()) {
  return z
    .object({
      firstName: z.string().trim().min(1, "Enter the first name.").max(100, "Use 100 characters or fewer."),
      middleName: blankToNull(100, "Middle name"),
      lastName: z.string().trim().min(1, "Enter the surname.").max(100, "Use 100 characters or fewer."),
      dateOfBirth: z.string().transform((v) => (v === "" ? null : v)),
      sex: z.union([z.enum(SEXES), z.literal("")]).transform((v) => (v === "" ? null : v)),
      stateOfOrigin: blankToNull(100, "State of origin"),
      lga: blankToNull(100, "LGA"),
      homeAddress: blankToNull(500, "Address"),
      house: blankToNull(100, "House"),
      medicalNotes: blankToNull(1000, "Medical notes"),
      classRoomId: z.string().transform((v) => (v === "" ? null : v)),
      admittedOn: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "Pick the admission date."),
    })
    .superRefine((v, ctx) => {
      if (v.admittedOn > addYears(today, 1)) {
        ctx.addIssue({ code: "custom", path: ["admittedOn"], message: "Admission can be at most a year ahead." });
      }
      if (v.dateOfBirth) {
        if (v.dateOfBirth > today) ctx.addIssue({ code: "custom", path: ["dateOfBirth"], message: "Date of birth can't be in the future." });
        else if (v.admittedOn && (v.dateOfBirth > addYears(v.admittedOn, -1) || v.dateOfBirth < addYears(v.admittedOn, -25))) {
          ctx.addIssue({ code: "custom", path: ["dateOfBirth"], message: "The pupil must be 1–25 years old at admission." });
        }
      }
    });
}
export type StudentFormValues = z.input<ReturnType<typeof studentSchema>>;
export type StudentPayload = z.output<ReturnType<typeof studentSchema>>;

/**
 * Loose phone check. The API normalises to +234… and is the real judge; this only catches
 * obvious typos: 7–15 digits after removing spaces, brackets, dashes and a leading +.
 */
export function looksLikePhone(v: string): boolean {
  const digits = v.replace(/[\s()\-.]/g, "").replace(/^\+/, "");
  return /^\d{7,15}$/.test(digits);
}

/** Digits only, for "same phone" lookups: 0803… and +234803… both become 803…. */
export function phoneKey(v: string): string {
  const digits = v.replace(/\D/g, "");
  if (digits.startsWith("234")) return digits.slice(3);
  if (digits.startsWith("0")) return digits.slice(1);
  return digits;
}

export const guardianDetailsSchema = z.object({
  fullName: z.string().trim().min(1, "Enter the guardian's name.").max(150, "Use 150 characters or fewer."),
  phone: z.string().trim().min(1, "Enter a phone number.").refine(looksLikePhone, "Enter a valid phone number, e.g. 0803 000 0000."),
  email: z
    .string()
    .trim()
    .max(256, "Use 256 characters or fewer.")
    .refine((v) => v === "" || z.email().safeParse(v).success, "Enter a valid email address.")
    .transform((v) => (v === "" ? null : v)),
  occupation: blankToNull(100, "Occupation"),
  address: blankToNull(500, "Address"),
});
export type GuardianDetailsValues = z.input<typeof guardianDetailsSchema>;
export type GuardianDetailsPayload = z.output<typeof guardianDetailsSchema>;

export const guardianLinkSchema = z.object({
  relationship: z.enum(RELATIONSHIPS),
  isPrimaryContact: z.boolean(),
  canPickUp: z.boolean(),
});
export type GuardianLinkValues = z.infer<typeof guardianLinkSchema>;

export const addGuardianSchema = guardianDetailsSchema.extend(guardianLinkSchema.shape);
export type AddGuardianValues = z.input<typeof addGuardianSchema>;
export type AddGuardianPayload = z.output<typeof addGuardianSchema>;

/** Allowed status changes: Active ⇄ Suspended; Active/Suspended → Withdrawn/Graduated; Withdrawn → Active. Graduated is final. */
export function nextStatuses(current: StudentStatus): StudentStatus[] {
  switch (current) {
    case "Active":
      return ["Suspended", "Withdrawn", "Graduated"];
    case "Suspended":
      return ["Active", "Withdrawn", "Graduated"];
    case "Withdrawn":
      return ["Active"];
    default:
      return [];
  }
}

export const statusTone: Record<StudentStatus, "success" | "warning" | "neutral" | "brand"> = {
  Active: "success",
  Suspended: "warning",
  Withdrawn: "neutral",
  Graduated: "brand",
};

export const statusActionLabel: Record<StudentStatus, string> = {
  Active: "Re-admit / reinstate",
  Suspended: "Suspend",
  Withdrawn: "Withdraw",
  Graduated: "Mark as graduated",
};

export function statusConsequence(from: StudentStatus, to: StudentStatus): string {
  if (to === "Active") return from === "Withdrawn" ? "They'll be re-admitted. Their class must have room." : "Their access is restored.";
  const final = to === "Graduated" ? " Graduation is final and can't be undone." : "";
  return `Their login is disabled straight away and they disappear from their parents' portal. Records are kept.${final}`;
}

export type StudentSearch = { search?: string; classRoomId?: string; status: StudentStatus | "all"; page: number };

export function parseStudentSearch(params: Record<string, string | string[] | undefined>): StudentSearch {
  const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v)?.trim() || undefined;
  const status = one(params.status);
  const page = Number.parseInt(one(params.page) ?? "1", 10);
  const classRoomId = one(params.classRoomId);
  return {
    search: one(params.search)?.slice(0, 100),
    classRoomId: classRoomId && /^[0-9a-f-]{36}$/i.test(classRoomId) ? classRoomId : undefined,
    status: status === "all" ? "all" : (STUDENT_STATUSES as readonly string[]).includes(status ?? "") ? (status as StudentStatus) : "Active",
    page: Number.isFinite(page) && page > 0 ? page : 1,
  };
}

export function studentsHref(s: StudentSearch, overrides: Partial<StudentSearch> = {}): string {
  const m = { ...s, ...overrides };
  const q = new URLSearchParams();
  if (m.search) q.set("search", m.search);
  if (m.classRoomId) q.set("classRoomId", m.classRoomId);
  if (m.status !== "Active") q.set("status", m.status);
  if (m.page > 1) q.set("page", String(m.page));
  const qs = q.toString();
  return qs ? `/admin/students?${qs}` : "/admin/students";
}

/** Age in whole years on a given day (default today), from YYYY-MM-DD. */
export function ageOn(dob: string, on = schoolToday()): number {
  const [y, m, d] = dob.split("-").map(Number);
  const [ty, tm, td] = on.split("-").map(Number);
  return ty - y - (tm < m || (tm === m && td < d) ? 1 : 0);
}

export function guardiansHref(search: string | undefined, page = 1): string {
  const q = new URLSearchParams();
  if (search) q.set("search", search);
  if (page > 1) q.set("page", String(page));
  const qs = q.toString();
  return qs ? `/admin/guardians?${qs}` : "/admin/guardians";
}
