import type { Metadata } from "next";
import { Lock, Users } from "lucide-react";
import { load } from "@/lib/api/server";
import { requireSessionUser } from "@/lib/auth/session";
import { hasAnyRole } from "@/lib/auth/roles";
import { GUID } from "@/lib/media/binary-proxy";
import { PromotionBoard } from "@/components/promotion/promotion-board";
import { Button, Card, PageHeader, Select } from "@/components/ui/primitives";
import { AccessDenied, Alert, EmptyState, ProblemState } from "@/components/ui/states";

export const metadata: Metadata = { title: "Promotion" };

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v) || undefined;

/** E3.6 admin: move every class on at the end of the session (promote, repeat, graduate, withdraw), with undo per class. */
export default async function PromotionPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const user = await requireSessionUser();
  const header = <PageHeader title="Promotion" description="Move every class on at the end of the session. Students are pre-filled; change the exceptions, then apply." />;
  if (!hasAnyRole(user.roles, ["Admin"])) {
    return (
      <>
        {header}
        <Card>
          <AccessDenied />
        </Card>
      </>
    );
  }
  const sp = await searchParams;
  const sessionId = GUID.test(one(sp.session) ?? "") ? one(sp.session) : undefined;
  const [sessions, plan, classrooms] = await Promise.all([
    load((api) => api.GET("/api/academics/sessions")),
    load((api) => api.GET("/api/promotions", { params: { query: sessionId ? { sessionId } : {} } })),
    load((api) => api.GET("/api/academics/classrooms")),
  ]);
  const classOptions = classrooms.ok
    ? [...classrooms.data].sort((a, b) => a.levelOrder - b.levelOrder || a.name.localeCompare(b.name)).map((c) => ({ id: c.id, name: c.name, levelOrder: c.levelOrder, capacity: c.capacity, studentCount: c.studentCount }))
    : [];

  return (
    <>
      {header}
      {sessions.ok && sessions.data.length > 1 && (
        <form action="/admin/academics/promotion" className="mb-5 flex flex-wrap items-end gap-3">
          <div className="space-y-1.5 text-sm">
            <label htmlFor="pr-session" className="block font-medium">
              Session
            </label>
            <Select id="pr-session" name="session" defaultValue={plan.ok ? plan.data.sessionId : (sessionId ?? "")}>
              {sessions.data.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                  {s.isCurrent ? " (current)" : ""}
                </option>
              ))}
            </Select>
          </div>
          <Button type="submit" variant="secondary">
            Show
          </Button>
        </form>
      )}

      {!plan.ok ? (
        <Card>
          <ProblemState problem={plan.problem} />
        </Card>
      ) : (
        <>
          {!plan.data.canPromote && (
            <Alert tone="info" title={`Promotion for ${plan.data.session} isn't open yet`} className="mb-5">
              <span className="inline-flex items-start gap-2">
                <Lock className="mt-0.5 size-4 shrink-0" aria-hidden />
                {plan.data.closedReason ?? "It opens when the third term starts."} You can still look through the preview.
              </span>
            </Alert>
          )}
          {plan.data.classes.length === 0 ? (
            <Card>
              <EmptyState icon={Users} title="No classes with students">
                Students in classes show here at the end of the session.
              </EmptyState>
            </Card>
          ) : (
            <PromotionBoard
              // Remount after an apply or undo so pending rows pick up fresh suggestions.
              key={`${plan.data.sessionId}:${plan.data.classes.flatMap((c) => c.students.filter((s) => s.done).map((s) => s.studentId)).join(",")}`}
              plan={plan.data}
              classOptions={classOptions}
            />
          )}
          <p className="mt-6 text-sm text-muted">
            Past report cards keep the class they were earned in. Create the new session and make its first term current under Sessions &amp; terms as usual.
          </p>
        </>
      )}
    </>
  );
}
