import {
  BookOpen, BookOpenCheck, Bus, CalendarCheck, CalendarDays, ClipboardList, CreditCard, FileQuestion, FileText, GraduationCap, Home, Image as ImageIcon,
  Inbox, LayoutDashboard, Layers, ListChecks, Menu, Newspaper, Receipt, School, ScrollText, Settings2, ShieldCheck, TableProperties,
  TriangleAlert, UserPlus, Users, UsersRound, Wallet, type LucideIcon,
} from "lucide-react";
import type { IconName } from "./nav";

const icons: Record<IconName, LucideIcon> = {
  dashboard: LayoutDashboard, students: GraduationCap, guardians: UsersRound, staff: Users, classes: Layers,
  subjects: BookOpen, sessions: CalendarDays, scores: TableProperties, reports: FileText, attendance: CalendarCheck,
  fees: Wallet, invoices: Receipt, payments: CreditCard, debtors: TriangleAlert, admissions: UserPlus, news: Newspaper,
  events: CalendarDays, gallery: ImageIcon, messages: Inbox, school: School, assessment: Settings2, users: ShieldCheck,
  audit: ScrollText, home: Home, results: ClipboardList, questions: FileQuestion, tests: ListChecks, more: Menu,
  transport: Bus, homework: BookOpenCheck,
};

export function NavIcon({ name, className }: { name: IconName; className?: string }) {
  const Icon = icons[name];
  return <Icon className={className ?? "size-4"} aria-hidden />;
}
