"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { CheckCheck, Lock, Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { ClassReportSummary } from "@/lib/api/types";
import { saveAttendance } from "@/lib/reports/actions";
import { attendanceErrors } from "@/lib/reports/rules";
import { runAction } from "@/lib/client/action";
import { Badge, Button, Card, Input, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

/**
 * Term attendance (manual until the daily register, E9): days school opened + days present per
 * student, saved for the class in one call. Published students are read-only.
 */
export function AttendanceForm({ summary }: { summary: ClassReportSummary }) {
  const router = useRouter();
  const editable = summary.rows.filter((r) => r.status !== "Published");
  const allLocked = editable.length === 0;
  const [opened, setOpened] = useState(summary.daysOpened?.toString() ?? "");
  const [present, setPresent] = useState<Record<string, string>>(() => Object.fromEntries(editable.map((r) => [r.studentId, r.daysPresent?.toString() ?? ""])));
  const [serverErrors, setServerErrors] = useState<Record<string, string>>({});
  const [problem, setProblem] = useState<Problem | null>(null);
  const [saving, setSaving] = useState(false);
  const [touched, setTouched] = useState(false);

  const errors = attendanceErrors(opened, present);
  const show = (key: string) => (touched ? errors[key] : undefined) ?? serverErrors[key];

  const markAll = () => {
    if (!/^\d+$/.test(opened.trim())) {
      setTouched(true);
      return;
    }
    setPresent(Object.fromEntries(editable.map((r) => [r.studentId, opened.trim()])));
  };

  const save = async () => {
    setTouched(true);
    setProblem(null);
    if (Object.keys(errors).length > 0) return;
    const entries = editable.map((r) => ({ studentId: r.studentId, daysPresent: Number(present[r.studentId]) }));
    setSaving(true);
    const saved = await runAction(() => saveAttendance({ classRoomId: summary.classRoomId, termId: summary.termId, daysOpened: Number(opened), entries }), {
      success: "Attendance saved",
      onProblem: (p) => {
        if (p.status === 400 && p.errors) {
          const mapped: Record<string, string> = {};
          for (const [k, msgs] of Object.entries(p.errors)) {
            const m = /^entries\[(\d+)\]\.daysPresent$/.exec(k);
            if (m && entries[Number(m[1])]) mapped[entries[Number(m[1])].studentId] = msgs.join(" ");
            else if (k === "daysOpened") mapped.daysOpened = msgs.join(" ");
          }
          setServerErrors(mapped);
        } else setProblem(p);
      },
    });
    setSaving(false);
    if (saved) {
      setServerErrors({});
      router.push(`/admin/reports/${summary.classRoomId}?termId=${summary.termId}`);
    }
  };

  return (
    <div className="space-y-4">
      {allLocked && (
        <Alert tone="info" title="Published">
          Every report in this class is published, so attendance is locked. Ask an admin to unpublish to make changes.
        </Alert>
      )}
      {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
        <Alert tone="danger" title="Couldn't save attendance">
          {problemMessage(problem)}
        </Alert>
      )}

      <Card className="flex flex-col gap-3 p-4 sm:flex-row sm:items-end">
        <div className="space-y-1.5">
          <label htmlFor="days-opened" className="block text-sm font-medium">
            Days school opened this term
          </label>
          <Input
            id="days-opened"
            value={opened}
            onChange={(e) => setOpened(e.target.value)}
            inputMode="numeric"
            readOnly={allLocked}
            aria-invalid={show("daysOpened") ? true : undefined}
            aria-describedby={show("daysOpened") ? "days-opened-error" : undefined}
            className="tabular w-32"
          />
          {show("daysOpened") && (
            <p id="days-opened-error" role="alert" className="text-sm text-danger">
              {show("daysOpened")}
            </p>
          )}
        </div>
        {!allLocked && (
          <Button variant="secondary" onClick={markAll}>
            <CheckCheck className="size-4" aria-hidden />
            Mark all present
          </Button>
        )}
      </Card>

      <Card className="overflow-hidden">
        <ul className="divide-y divide-border">
          {summary.rows.map((r) => {
            const locked = r.status === "Published";
            const err = locked ? undefined : show(r.studentId);
            const value = locked ? (r.daysPresent?.toString() ?? "") : (present[r.studentId] ?? "");
            const absent = /^\d+$/.test(opened) && /^\d+$/.test(value) ? Number(opened) - Number(value) : null;
            return (
              <li key={r.studentId} className={cn("flex flex-wrap items-center gap-3 px-4 py-2.5", locked && "bg-surface-2/40")}>
                <label htmlFor={`present-${r.studentId}`} className="min-w-0 flex-1">
                  <span className="block font-medium">{r.name}</span>
                  <span className="block font-mono text-xs text-muted">{r.admissionNumber}</span>
                </label>
                {locked && (
                  <Badge>
                    <Lock className="size-3" aria-hidden />
                    Published
                  </Badge>
                )}
                <div className="flex items-center gap-2">
                  <Input
                    id={`present-${r.studentId}`}
                    value={value}
                    onChange={(e) => setPresent((p) => ({ ...p, [r.studentId]: e.target.value }))}
                    readOnly={locked}
                    inputMode="numeric"
                    aria-label={`Days present for ${r.name}`}
                    aria-invalid={err ? true : undefined}
                    title={err}
                    className={cn("tabular w-20 text-center", err && "border-danger")}
                  />
                  <span className="w-28 text-xs text-muted">
                    {err ? <span className="text-danger">{err}</span> : absent !== null && absent >= 0 ? `${absent} absent` : `of ${opened || "?"}`}
                  </span>
                </div>
              </li>
            );
          })}
        </ul>
      </Card>

      {!allLocked && (
        <div className="sticky bottom-4 z-10 flex justify-end">
          <Button onClick={save} loading={saving} className="shadow-lg">
            <Save className="size-4" aria-hidden />
            Save attendance
          </Button>
        </div>
      )}
    </div>
  );
}
