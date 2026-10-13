import type { Metadata } from "next";
import Link from "next/link";
import { ArrowUpRight, CalendarCheck, CalendarDays, Layers, Command, GraduationCap, KeyRound, ScrollText, TableProperties, Wallet, type LucideIcon } from "lucide-react";
import { load } from "@/lib/api/server";
import { hasAnyRole, type Role } from "@/lib/auth/roles";
import { firstName } from "@/lib/flash";
import { Badge, Card, cn } from "@/components/ui/primitives";
import { ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Dashboard" };

function greeting(now: Date): string {
  const hour = Number(new Intl.DateTimeFormat("en-NG", { hour: "numeric", hour12: false, timeZone: "Africa/Lagos" }).format(now));
  if (hour < 12) return "Good morning";
  if (hour < 17) return "Good afternoon";
  return "Good evening";
}

type Tile = { title: string; body: string; icon: LucideIcon; roles: Role[]; href?: string };

const tiles: Tile[] = [
  { title: "Audit log", body: "Every change to school records: who, what, when and from where.", icon: ScrollText, roles: ["Admin"], href: "/admin/audit" },
  { title: "Change password", body: "Keep your account safe. Other devices get signed out.", icon: KeyRound, roles: ["Admin", "Teacher", "Bursar"], href: "/change-password" },
  { title: "Classes & subjects", body: "Year groups and arms, capacity, and the subjects each class offers.", icon: Layers, roles: ["Admin"], href: "/admin/academics/classes" },
  { title: "School calendar", body: "Sessions, terms and mid-term breaks, and which term is current.", icon: CalendarDays, roles: ["Admin", "Teacher", "Bursar"], href: "/admin/academics/sessions" },
  { title: "Students & guardians", body: "Admissions, class lists, guardians and sign-in slips.", icon: GraduationCap, roles: ["Admin", "Teacher", "Bursar"], href: "/admin/students" },
  { title: "Score entry", body: "Spreadsheet-style entry for CA and exams.", icon: TableProperties, roles: ["Admin", "Teacher"] },
  { title: "Attendance", body: "Morning register in a couple of taps.", icon: CalendarCheck, roles: ["Admin", "Teacher"] },
  { title: "Fees & payments", body: "Invoices, receipts and who still owes.", icon: Wallet, roles: ["Admin", "Bursar"] },
];

export default async function DashboardPage() {
  const me = await load((api) => api.GET("/api/auth/me"));
  if (!me.ok) return <ProblemState problem={me.problem} />;
  const user = me.data;
  const now = new Date();
  const today = new Intl.DateTimeFormat("en-NG", { weekday: "long", day: "numeric", month: "long", timeZone: "Africa/Lagos" }).format(now);
  const mine = tiles.filter((t) => hasAnyRole(user.roles, t.roles));

  return (
    <>
      <section className="brand-panel relative mb-6 overflow-hidden rounded-panel p-6 text-navy-ink sm:p-8">
        <p className="text-sm font-medium text-gold">{today}</p>
        <h1 className="mt-1 font-display text-3xl font-semibold sm:text-4xl">
          {greeting(now)}, <em className="text-gold">{firstName(user.fullName) || user.fullName}</em>
        </h1>
        <div className="mt-4 flex flex-wrap items-center gap-2">
          {user.roles.map((r) => (
            <span key={r} className="rounded-full bg-white/10 px-3 py-1 text-xs font-medium ring-1 ring-white/20">
              {r}
            </span>
          ))}
        </div>
        <p className="mt-5 hidden items-center gap-2 text-sm text-navy-ink/75 sm:flex">
          <Command className="size-4" aria-hidden />
          Tip: press <kbd className="rounded bg-white/15 px-1.5 py-0.5 text-xs">Ctrl K</kbd> to jump anywhere.
        </p>
      </section>

      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wider text-muted">Your workspace</h2>
      <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {mine.map(({ title, body, icon: Icon, href }, i) => {
          const inner = (
            <>
              <div className="flex items-start justify-between">
                <span className={cn("inline-flex size-10 items-center justify-center rounded-full", href ? "bg-brand/10 text-brand" : "bg-surface-2 text-muted")}>
                  <Icon className="size-5" aria-hidden />
                </span>
                {href ? (
                  <ArrowUpRight className="size-4 text-muted transition-transform duration-200 group-hover:translate-x-0.5 group-hover:-translate-y-0.5 group-hover:text-brand" aria-hidden />
                ) : (
                  <Badge>Coming soon</Badge>
                )}
              </div>
              <h3 className="mt-4 font-display text-lg font-semibold">{title}</h3>
              <p className="mt-1 text-sm text-muted">{body}</p>
            </>
          );
          return (
            <li key={title} className={cn("animate-rise", ["", "delay-1", "delay-2", "delay-3", "delay-4"][Math.min(i, 4)])}>
              {href ? (
                <Link href={href} className="group block h-full">
                  <Card className="card-interactive h-full p-5">{inner}</Card>
                </Link>
              ) : (
                <Card className="h-full p-5 opacity-80">{inner}</Card>
              )}
            </li>
          );
        })}
      </ul>
    </>
  );
}
