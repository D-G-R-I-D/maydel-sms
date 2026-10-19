import type { Metadata } from "next";
import { requireSessionUser } from "@/lib/auth/session";

export const metadata: Metadata = { title: { default: "Test", template: "%s · Maydel test" } };

/** E16.3: the exam room has no portal tabs or menus, so nothing pulls a student away mid-test. */
export default async function ExamLayout({ children }: { children: React.ReactNode }) {
  await requireSessionUser();
  return <div className="min-h-dvh bg-bg">{children}</div>;
}
