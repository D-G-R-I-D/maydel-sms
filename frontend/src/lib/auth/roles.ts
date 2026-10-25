import type { Role } from "@/lib/api/types";

export type { Role };

/** Every role the API knows. `satisfies` makes the type check fail if the contract adds or renames one. */
export const Roles = ["Admin", "Teacher", "Bursar", "Parent", "Student", "Applicant", "Driver"] as const satisfies readonly Role[];

export function isRole(value: unknown): value is Role {
  return typeof value === "string" && (Roles as readonly string[]).includes(value);
}

const staffRoles: readonly Role[] = ["Admin", "Teacher", "Bursar"];
const portalRoles: readonly Role[] = ["Parent", "Student"];
/** Families applying for a place (E11): new applicants, and existing parents applying for siblings. */
const admissionsRoles: readonly Role[] = ["Applicant", "Parent"];

export function hasAnyRole(roles: readonly string[], allowed: readonly Role[]): boolean {
  return roles.some((r) => (allowed as readonly string[]).includes(r));
}

export const isStaff = (roles: readonly string[]) => hasAnyRole(roles, staffRoles);
/** E18: bus drivers have their own phone page and clock-in, and no student records. */
export const isDriver = (roles: readonly string[]) => hasAnyRole(roles, ["Driver"]);
/** Everyone who clocks in at the kiosk (the API's Workforce policy). */
export const isWorkforce = (roles: readonly string[]) => isStaff(roles) || isDriver(roles);
export const isDriverPath = (pathname: string) => pathname === "/driver" || pathname.startsWith("/driver/");
export const isPortalUser = (roles: readonly string[]) => hasAnyRole(roles, portalRoles);
export const isAdmissionsUser = (roles: readonly string[]) => hasAnyRole(roles, admissionsRoles);
/** Sitting a computer-based test (E16.3) is for student accounts only; parents can't sit for a child. */
export const isStudent = (roles: readonly string[]) => hasAnyRole(roles, ["Student"]);
export const isExamPath = (pathname: string) => pathname === "/exam" || pathname.startsWith("/exam/");

/** Where a scanned kiosk QR lands (staff and drivers). The kiosk screen itself, /clock/kiosk, is public. */
export const isClockPath = (pathname: string) => pathname === "/clock";

/** The public "Buy admission form" page; the rest of /admissions needs an applicant or parent account. */
export const isPublicAdmissionsPath = (pathname: string) => pathname === "/admissions/apply" || pathname.startsWith("/admissions/apply/");

/** Where a user lands after sign-in: staff (even if also a parent) go to the console; applicant-only families to admissions. */
export function homeFor(roles: readonly string[]): string {
  if (isStaff(roles)) return "/admin";
  if (isDriver(roles)) return "/driver";
  if (isPortalUser(roles)) return "/portal";
  if (hasAnyRole(roles, ["Applicant"])) return "/admissions";
  return "/login";
}

/** UX guard only — the API is the security boundary. Returns true when the area allows these roles. */
export function canEnter(pathname: string, roles: readonly string[]): boolean {
  if (pathname === "/admin" || pathname.startsWith("/admin/")) return isStaff(roles);
  if (pathname === "/portal" || pathname.startsWith("/portal/")) return isPortalUser(roles);
  if (isExamPath(pathname)) return isStudent(roles);
  if (isClockPath(pathname)) return isWorkforce(roles);
  if (isDriverPath(pathname)) return isDriver(roles) || hasAnyRole(roles, ["Admin"]);
  if ((pathname === "/admissions" || pathname.startsWith("/admissions/")) && !isPublicAdmissionsPath(pathname)) return isAdmissionsUser(roles);
  return true;
}

/**
 * Accepts only same-site relative paths for `?next=` so a crafted link can't bounce
 * a freshly signed-in user to another origin.
 */
export function safeNext(next: string | null | undefined): string | null {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/\\")) return null;
  if (next.startsWith("/api/") || next === "/login" || next.startsWith("/login?")) return null;
  return next;
}
