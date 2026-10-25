import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { ArrowLeft, CalendarDays, QrCode } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { homeFor, isStaff } from "@/lib/auth/roles";
import { firstName } from "@/lib/flash";
import { ClockCodes, cleanCode, hours, shortDay, stateLabel, stateTone, watTime } from "@/lib/clock/clock";
import { ClockCard } from "@/components/clock/clock-card";
import { ThemeToggle } from "@/components/shell/theme-toggle";
import { Badge, Card, buttonClasses } from "@/components/ui/primitives";
import { EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Clock in", robots: { index: false, follow: false } };

/**
 * Where a staff member lands after scanning the kiosk QR (`/clock?c=…`). The scan is sent from the
 * browser once the page opens; below it, the last 30 days of their own clock-ins.
 */
export default async function ClockPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const code = cleanCode((await searchParams).c);
  const mine = await load((api) => api.GET("/api/clock/me"));
  const notStaff = !mine.ok && mine.problem.code === ClockCodes.notStaff;

  return (
    <div className="min-h-dvh bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex h-16 max-w-2xl items-center gap-3 px-4">
          <Image src="/brand/crest.svg" alt="" width={28} height={35} className="h-auto w-7" />
          <p className="min-w-0 flex-1 truncate font-display font-semibold text-brand">Staff clock-in</p>
          <ThemeToggle />
          <Link href={homeFor(user.roles)} className={buttonClasses("secondary", "sm")}>
            <ArrowLeft className="size-4" aria-hidden />
            {isStaff(user.roles) ? "Console" : "Back"}
          </Link>
        </div>
      </header>
      <main id="main" className="mx-auto max-w-2xl space-y-6 px-4 py-6">
        <div>
          <h1 className="font-display text-2xl font-semibold">Hello, {firstName(user.fullName) || user.fullName}</h1>
          <p className="text-sm text-muted">Scan the code on the school screen when you arrive and again when you leave.</p>
        </div>

        {code && !notStaff ? (
          <ClockCard code={code} />
        ) : !notStaff ? (
          <Card className="flex items-center gap-4 p-5">
            <QrCode className="size-8 shrink-0 text-brand" aria-hidden />
            <p className="text-sm text-muted">To clock in or out, scan the QR code on the screen at the school entrance with your phone camera.</p>
          </Card>
        ) : null}

        <section aria-labelledby="mine" className="space-y-3">
          <h2 id="mine" className="font-display text-lg font-semibold">
            My attendance
          </h2>
          {!mine.ok ? (
            <Card>
              {notStaff ? (
                <EmptyState icon={CalendarDays} title="No staff record on this account">
                  Clock-in is for staff with a staff record. Ask the school admin to link your account.
                </EmptyState>
              ) : (
                <ProblemState problem={mine.problem} />
              )}
            </Card>
          ) : mine.data.length === 0 ? (
            <Card>
              <EmptyState icon={CalendarDays} title="Nothing in the last 30 days">
                Your clock-ins will show here.
              </EmptyState>
            </Card>
          ) : (
            <Card className="divide-y divide-border p-0">
              {mine.data.map((d) => (
                <div key={d.date} className="flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-3 text-sm">
                  <span className="w-28 font-medium">{shortDay(d.date)}</span>
                  <span className="tabular flex-1 text-muted">
                    {watTime(d.clockInAt)} – {watTime(d.clockOutAt)}
                    {d.hoursWorked != null && <span className="ml-2">({hours(d.hoursWorked)})</span>}
                  </span>
                  <Badge tone={stateTone[d.state]}>{d.isLate && d.state === "Out" ? "Late, out" : stateLabel[d.state]}</Badge>
                </div>
              ))}
            </Card>
          )}
          <p className="text-xs text-muted">Times are Abuja time (WAT). If a day is wrong, ask the school admin to correct it.</p>
        </section>
      </main>
    </div>
  );
}
