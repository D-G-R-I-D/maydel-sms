import type { Role } from "@/lib/auth/roles";

export type IconName =
  | "dashboard" | "students" | "guardians" | "staff" | "classes" | "subjects" | "sessions" | "scores" | "reports"
  | "attendance" | "fees" | "invoices" | "payments" | "debtors" | "admissions" | "news" | "events" | "gallery"
  | "messages" | "school" | "assessment" | "users" | "audit" | "home" | "results" | "more";

export type NavItem = {
  label: string;
  href: string;
  icon: IconName;
  roles: readonly Role[];
  /** false until the page ships; rendered as a disabled "Soon" item. */
  ready?: boolean;
  /** A count shown beside the label (e.g. proofs waiting to be checked); filled in by the layout. */
  badge?: number;
};
export type NavGroup = { label: string; items: NavItem[] };

const A: Role[] = ["Admin"];
const AT: Role[] = ["Admin", "Teacher"];
const AB: Role[] = ["Admin", "Bursar"];
const STAFF: Role[] = ["Admin", "Teacher", "Bursar"];

/** Staff console IA (docs/05-design-system.md §4). */
export const consoleNav: NavGroup[] = [
  { label: "Overview", items: [{ label: "Dashboard", href: "/admin", icon: "dashboard", roles: STAFF, ready: true }] },
  {
    label: "People",
    items: [
      { label: "Students", href: "/admin/students", icon: "students", roles: STAFF, ready: true },
      { label: "Guardians", href: "/admin/guardians", icon: "guardians", roles: A, ready: true },
      { label: "Staff", href: "/admin/staff", icon: "staff", roles: A, ready: true },
    ],
  },
  {
    label: "Academics",
    items: [
      { label: "Classes", href: "/admin/academics/classes", icon: "classes", roles: A, ready: true },
      { label: "Subjects", href: "/admin/academics/subjects", icon: "subjects", roles: A, ready: true },
      { label: "Sessions & terms", href: "/admin/academics/sessions", icon: "sessions", roles: STAFF, ready: true },
      { label: "Score entry", href: "/admin/assessment", icon: "scores", roles: AT, ready: true },
      { label: "Reports", href: "/admin/reports", icon: "reports", roles: AT, ready: true },
    ],
  },
  { label: "Attendance", items: [{ label: "Attendance", href: "/admin/attendance", icon: "attendance", roles: AT, ready: true }] },
  {
    label: "Finance",
    items: [
      { label: "Fee items", href: "/admin/fees", icon: "fees", roles: AB, ready: true },
      { label: "Invoices", href: "/admin/invoices", icon: "invoices", roles: AB, ready: true },
      { label: "To verify", href: "/admin/proofs", icon: "payments", roles: AB, ready: true },
      { label: "Payments", href: "/admin/payments", icon: "payments", roles: AB, ready: true },
      { label: "Debtors", href: "/admin/debtors", icon: "debtors", roles: AB, ready: true },
    ],
  },
  { label: "Admissions", items: [{ label: "Admissions", href: "/admin/admissions", icon: "admissions", roles: A }] },
  {
    label: "Website",
    items: [
      { label: "News", href: "/admin/news", icon: "news", roles: A },
      { label: "Events", href: "/admin/events", icon: "events", roles: A },
      { label: "Gallery", href: "/admin/gallery", icon: "gallery", roles: A },
      { label: "Messages", href: "/admin/messages", icon: "messages", roles: A },
    ],
  },
  {
    label: "Settings",
    items: [
      { label: "School profile", href: "/admin/settings/school", icon: "school", roles: A, ready: true },
      { label: "Assessment", href: "/admin/settings/assessment", icon: "assessment", roles: AT, ready: true },
      { label: "Fees", href: "/admin/settings/fees", icon: "fees", roles: AB, ready: true },
      { label: "Users", href: "/admin/settings/users", icon: "users", roles: A },
    ],
  },
  { label: "Audit", items: [{ label: "Audit log", href: "/admin/audit", icon: "audit", roles: A, ready: true }] },
];

const PORTAL: Role[] = ["Parent", "Student"];

/** Portal tabs: bottom bar on mobile (design system §4). */
export const portalNav: NavItem[] = [
  { label: "Home", href: "/portal", icon: "home", roles: PORTAL, ready: true },
  { label: "Results", href: "/portal/results", icon: "results", roles: PORTAL, ready: true },
  { label: "Fees", href: "/portal/fees", icon: "fees", roles: PORTAL, ready: true },
  { label: "Attendance", href: "/portal/attendance", icon: "attendance", roles: PORTAL },
  { label: "More", href: "/portal/more", icon: "more", roles: PORTAL },
];

export function filterNav(groups: NavGroup[], roles: readonly string[]): NavGroup[] {
  return groups
    .map((g) => ({ ...g, items: g.items.filter((i) => i.roles.some((r) => roles.includes(r))) }))
    .filter((g) => g.items.length > 0);
}

/** Puts counts on nav items by href (e.g. { "/admin/proofs": 3 }); zero shows nothing. */
export function withBadges(groups: NavGroup[], badges: Record<string, number>): NavGroup[] {
  return groups.map((g) => ({ ...g, items: g.items.map((i) => (badges[i.href] ? { ...i, badge: badges[i.href] } : i)) }));
}

/** Longest matching href wins, so /admin doesn't light up on /admin/audit. */
export function activeHref(pathname: string, hrefs: readonly string[]): string | undefined {
  return hrefs
    .filter((h) => pathname === h || pathname.startsWith(`${h}/`))
    .sort((a, b) => b.length - a.length)[0];
}
