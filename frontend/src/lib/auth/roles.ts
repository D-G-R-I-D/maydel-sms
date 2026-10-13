import type { Role } from "@/lib/api/types";

export type { Role };

/** Every role the API knows. `satisfies` makes the type check fail if the contract adds or renames one. */
export const Roles = ["Admin", "Teacher", "Bursar", "Parent", "Student"] as const satisfies readonly Role[];

export function isRole(value: unknown): value is Role {
  return typeof value === "string" && (Roles as readonly string[]).includes(value);
}

const staffRoles: readonly Role[] = ["Admin", "Teacher", "Bursar"];
const portalRoles: readonly Role[] = ["Parent", "Student"];

export function hasAnyRole(roles: readonly string[], allowed: readonly Role[]): boolean {
  return roles.some((r) => (allowed as readonly string[]).includes(r));
}

export const isStaff = (roles: readonly string[]) => hasAnyRole(roles, staffRoles);
export const isPortalUser = (roles: readonly string[]) => hasAnyRole(roles, portalRoles);

/** Where a user lands after sign-in: staff (even if also a parent) go to the console. */
export function homeFor(roles: readonly string[]): string {
  if (isStaff(roles)) return "/admin";
  if (isPortalUser(roles)) return "/portal";
  return "/login";
}

/** UX guard only — the API is the security boundary. Returns true when the area allows these roles. */
export function canEnter(pathname: string, roles: readonly string[]): boolean {
  if (pathname === "/admin" || pathname.startsWith("/admin/")) return isStaff(roles);
  if (pathname === "/portal" || pathname.startsWith("/portal/")) return isPortalUser(roles);
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
