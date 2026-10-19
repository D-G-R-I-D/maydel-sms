import type { Metadata } from "next";
import Link from "next/link";
import { ChevronLeft, ChevronRight, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { Card, PageHeader, buttonClasses } from "@/components/ui/primitives";
import { AccessDenied, EmptyState, ProblemState } from "@/components/ui/states";
import { AiSettingsForm, AllowanceInput } from "@/components/cbt/ai-settings-form";
import { HBar } from "@/components/cbt/bars";

export const metadata: Metadata = { title: "AI questions" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;
const monthName = new Intl.DateTimeFormat("en-NG", { month: "long", year: "numeric", timeZone: "UTC" });

/** E16.9 (admin): switch AI question writing on/off, set allowances, and see this month's use. */
export default async function AiSettingsPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        <PageHeader title="AI questions" />
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  // The month in Abuja time (UTC+1); ?year=&month= picks another.
  const nowWat = new Date(new Date().getTime() + 3600 * 1000);
  const year = Number(one(sp.year)) || nowWat.getUTCFullYear();
  const month = Math.min(12, Math.max(1, Number(one(sp.month)) || nowWat.getUTCMonth() + 1));
  const [settings, usage] = await Promise.all([load((api) => api.GET("/api/cbt/ai/settings")), load((api) => api.GET("/api/cbt/ai/usage", { params: { query: { year, month } } }))]);
  const shift = (d: number) => {
    const m = new Date(Date.UTC(year, month - 1 + d, 1));
    return `/admin/settings/ai?year=${m.getUTCFullYear()}&month=${m.getUTCMonth() + 1}`;
  };
  const isCurrent = year === nowWat.getUTCFullYear() && month === nowWat.getUTCMonth() + 1;

  return (
    <>
      <PageHeader title="AI questions" description="Teachers can ask Maydel AI Assistant to draft questions from their own notes. Only their material is sent, never student information." />
      <div className="space-y-5">
        {settings.ok ? <AiSettingsForm initial={settings.data} /> : <Card><ProblemState problem={settings.problem} /></Card>}

        <Card className="overflow-hidden">
          <div className="flex flex-wrap items-center gap-3 border-b border-border p-5">
            <h2 className="min-w-0 flex-1 font-display text-lg font-semibold">Use in {monthName.format(new Date(Date.UTC(year, month - 1, 15)))}</h2>
            <Link href={shift(-1)} className={buttonClasses("secondary", "sm")} aria-label="Previous month">
              <ChevronLeft className="size-4" aria-hidden />
            </Link>
            {isCurrent ? (
              <span className={`${buttonClasses("secondary", "sm")} pointer-events-none opacity-50`} aria-disabled="true">
                <ChevronRight className="size-4" aria-hidden />
                <span className="sr-only">Next month</span>
              </span>
            ) : (
              <Link href={shift(1)} className={buttonClasses("secondary", "sm")} aria-label="Next month">
                <ChevronRight className="size-4" aria-hidden />
              </Link>
            )}
          </div>
          {!usage.ok ? (
            <ProblemState problem={usage.problem} />
          ) : (
            <>
              <div className="space-y-2 border-b border-border p-5">
                <p className="flex justify-between text-sm">
                  <span>School</span>
                  <span className="tabular font-medium">
                    {usage.data.schoolUsed} {usage.data.schoolCap > 0 ? `of ${usage.data.schoolCap}` : "used (no school limit)"}
                  </span>
                </p>
                {usage.data.schoolCap > 0 && <HBar ratio={usage.data.schoolUsed / usage.data.schoolCap} tone={usage.data.schoolUsed >= usage.data.schoolCap ? "danger" : "brand"} />}
                <p className="tabular text-xs text-muted">
                  {usage.data.inputTokens.toLocaleString("en-NG")} tokens in · {usage.data.outputTokens.toLocaleString("en-NG")} tokens out
                </p>
              </div>
              {usage.data.teachers.length === 0 ? (
                <EmptyState icon={Users} title="No teachers yet" />
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[40rem] text-sm">
                    <thead className="text-left text-xs uppercase tracking-wider text-muted">
                      <tr className="border-b border-border">
                        <th className="px-5 py-2.5 font-medium">Teacher</th>
                        <th className="px-3 py-2.5 font-medium">Used</th>
                        <th className="px-3 py-2.5 text-right font-medium">Tokens</th>
                        <th className="px-5 py-2.5 font-medium">Monthly allowance</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-border">
                      {usage.data.teachers.map((t) => (
                        <tr key={t.userId}>
                          <td className="px-5 py-2.5 font-medium">{t.name}</td>
                          <td className="px-3 py-2.5">
                            <span className="tabular">
                              {t.used} of {t.allowance}
                            </span>
                            <HBar ratio={t.allowance > 0 ? t.used / t.allowance : 0} tone={t.used >= t.allowance ? "warning" : "brand"} className="mt-1 w-28" />
                          </td>
                          <td className="tabular px-3 py-2.5 text-right text-muted">{(t.inputTokens + t.outputTokens).toLocaleString("en-NG")}</td>
                          <td className="px-5 py-2.5">
                            <AllowanceInput row={t} defaultAllowance={settings.ok ? settings.data.defaultMonthlyGenerations : t.allowance} />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          )}
        </Card>
      </div>
    </>
  );
}
