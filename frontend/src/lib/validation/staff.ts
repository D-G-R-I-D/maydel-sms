import { z } from "zod";
import type { Role, StaffLookupItem } from "@/lib/api/types";

/** Client mirror of the M1.1 part 2 staff rules (docs/handoffs/m1.1-staff.md). The API re-checks. */

export const STAFF_ROLES = ["Admin", "Teacher", "Bursar"] as const satisfies readonly Role[];
export type StaffRole = (typeof STAFF_ROLES)[number];

const optionalText = (max: number, message: string) =>
  z
    .string()
    .trim()
    .max(max, message)
    .transform((v) => (v === "" ? null : v));

export const staffSchema = z.object({
  fullName: z.string().trim().min(1, "Enter the full name.").max(150, "Use 150 characters or fewer."),
  email: z.string().trim().min(1, "Enter an email address.").max(256, "Use 256 characters or fewer.").email("Enter a valid email address."),
  phone: z
    .string()
    .trim()
    .refine((v) => v === "" || /^\+?[0-9 ()-]{7,32}$/.test(v), "Use digits, spaces, brackets or dashes, e.g. +234 803 123 4567.")
    .transform((v) => (v === "" ? null : v)),
  jobTitle: optionalText(100, "Use 100 characters or fewer."),
  roles: z.array(z.enum(STAFF_ROLES)).min(1, "Choose at least one role."),
  isActive: z.boolean(),
});
export type StaffFormValues = z.input<typeof staffSchema>;
export type StaffPayload = z.output<typeof staffSchema>;

export const roleDescriptions: Record<StaffRole, string> = {
  Admin: "Full access, including settings, staff and the audit log.",
  Teacher: "Classes, score entry, attendance and report comments.",
  Bursar: "Fees, invoices and payments.",
};

/**
 * Options for a teacher picker. The lookup lists active staff only, so a currently assigned
 * teacher who has since been deactivated is kept as an extra, labelled option.
 */
export function teacherOptions(lookup: readonly StaffLookupItem[], current: { id: string | null; name: string | null }) {
  const options = lookup.map((s) => ({ id: s.id, label: s.jobTitle ? `${s.fullName} · ${s.jobTitle}` : s.fullName }));
  if (current.id && !lookup.some((s) => s.id === current.id)) {
    options.unshift({ id: current.id, label: `${current.name ?? "Unknown"} (inactive)` });
  }
  return options;
}

const lastSeenFormat = new Intl.DateTimeFormat("en-NG", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false, timeZone: "Africa/Lagos" });

export function formatLastSeen(iso: string | null): string {
  if (!iso) return "Never";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : lastSeenFormat.format(d);
}

/** Is this staff record the signed-in admin? Staff logins use the email as username. */
export function isSelf(staffEmail: string, signedInUserName: string): boolean {
  return staffEmail.trim().toLowerCase() === signedInUserName.trim().toLowerCase();
}

export type StaffSearch = { search?: string; status: "active" | "inactive" | "all"; page: number };

export function parseStaffSearch(params: Record<string, string | string[] | undefined>): StaffSearch {
  const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v)?.trim() || undefined;
  const status = one(params.status);
  const page = Number.parseInt(one(params.page) ?? "1", 10);
  return {
    search: one(params.search)?.slice(0, 100),
    status: status === "inactive" || status === "all" ? status : "active",
    page: Number.isFinite(page) && page > 0 ? page : 1,
  };
}

export function staffHref(s: StaffSearch, overrides: Partial<StaffSearch> = {}): string {
  const m = { ...s, ...overrides };
  const q = new URLSearchParams();
  if (m.search) q.set("search", m.search);
  if (m.status !== "active") q.set("status", m.status);
  if (m.page > 1) q.set("page", String(m.page));
  const qs = q.toString();
  return qs ? `/admin/staff?${qs}` : "/admin/staff";
}
