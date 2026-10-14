"use client";

import Link from "next/link";
import { useState } from "react";
import { AlertTriangle, CalendarCheck, Check, ChevronRight, LockOpen, Minus, Printer, Send } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { ClassReportSummary, PublishResult } from "@/lib/api/types";
import { publishClass, unpublishReports } from "@/lib/reports/actions";
import { attendanceText, classPdfHref, formatAverage, ordinal, publishPreview } from "@/lib/reports/rules";
import { runAction } from "@/lib/client/action";
import { Badge, Button, buttonClasses, cn } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";

function Tick({ on, label }: { on: boolean; label: string }) {
  return on ? (
    <Check className="mx-auto size-4 text-success" aria-label={`${label}: written`} />
  ) : (
    <Minus className="mx-auto size-4 text-muted/60" aria-label={`${label}: not written`} />
  );
}

/** Class report list with (admin) publish / unpublish. Rows open the report card preview. */
export function ClassReports({ summary, isAdmin, canAttendance }: { summary: ClassReportSummary; isAdmin: boolean; canAttendance: boolean }) {
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
  const [problem, setProblem] = useState<Problem | null>(null);
  const [result, setResult] = useState<PublishResult | null>(null);
  const preview = publishPreview(summary);
  const href = (studentId: string) => `/admin/reports/${summary.classRoomId}/${studentId}?termId=${summary.termId}`;
  const attendanceHref = `/admin/reports/${summary.classRoomId}/attendance?termId=${summary.termId}`;
  const publishedSelected = summary.rows.filter((r) => selected.has(r.studentId) && r.status === "Published").map((r) => r.studentId);

  const toggle = (id: string) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return (
    <div className="space-y-4">
      {problem && (
        <Alert tone="danger" title={problem.code === "report.attendance_missing" ? "Attendance is missing" : "Couldn't publish"}>
          <p>{problemMessage(problem)}</p>
          {problem.code === "report.attendance_missing" && canAttendance && (
            <Link href={attendanceHref} className="mt-2 inline-flex items-center gap-1 font-medium text-brand hover:underline">
              <CalendarCheck className="size-4" aria-hidden />
              Enter attendance
            </Link>
          )}
        </Alert>
      )}
      {result && (
        <Alert tone="success" title={`${result.published} ${result.published === 1 ? "report" : "reports"} published`}>
          <p>Class average {formatAverage(result.classAverage)}. Scores, comments and attendance for these students are now locked.</p>
          {result.skipped.length > 0 && (
            <div className="mt-2">
              <p className="font-medium">Skipped:</p>
              <ul className="list-disc pl-5">
                {result.skipped.map((s) => (
                  <li key={s.studentId}>
                    {s.name}: {s.reason}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </Alert>
      )}

      <div className="flex flex-wrap items-center gap-2">
          <a href={classPdfHref(summary.classRoomId, summary.termId)} target="_blank" rel="noopener" className={buttonClasses("secondary")}>
            <Printer className="size-4" aria-hidden />
            Download class PDF (print)
          </a>
          {canAttendance && (
            <Link href={attendanceHref} className={buttonClasses("secondary")}>
              <CalendarCheck className="size-4" aria-hidden />
              Attendance
              {summary.daysOpened === null && <Badge tone="warning">Not entered</Badge>}
            </Link>
          )}
          {isAdmin && (
            <>
              <ConfirmDialog
                title={`Publish ${summary.classRoom} reports?`}
                confirmLabel="Publish"
                trigger={
                  <Button disabled={preview.willPublish === 0}>
                    <Send className="size-4" aria-hidden />
                    Publish class
                  </Button>
                }
                onConfirm={async () => {
                  setProblem(null);
                  setResult(null);
                  const r = await runAction(() => publishClass(summary.classRoomId, summary.termId), {
                    success: (x) => `${x.published} reports published`,
                    onProblem: setProblem,
                  });
                  if (r) setResult(r);
                  return true;
                }}
              >
                <p>
                  <span className="font-medium text-text">{preview.willPublish}</span> {preview.willPublish === 1 ? "report" : "reports"} will be published
                  {preview.noScores.length > 0 && (
                    <>
                      ; <span className="font-medium text-text">{preview.noScores.length}</span> {preview.noScores.length === 1 ? "has" : "have"} no scores and will be skipped
                    </>
                  )}
                  .
                </p>
                <p className="mt-2">Figures are frozen, and scores, comments and attendance lock. Parents will see published reports.</p>
                {preview.missingAttendance.length > 0 && (
                  <p className="mt-2 flex items-start gap-1.5 text-warning">
                    <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
                    Attendance is missing for {preview.missingAttendance.length} {preview.missingAttendance.length === 1 ? "student" : "students"}; publishing will be refused until it&apos;s entered.
                  </p>
                )}
              </ConfirmDialog>
              {summary.publishedCount > 0 && (
                <ConfirmDialog
                  title={publishedSelected.length > 0 ? `Unpublish ${publishedSelected.length} selected?` : `Unpublish all of ${summary.classRoom}?`}
                  confirmLabel="Unpublish"
                  tone="destructive"
                  trigger={
                    <Button variant="secondary">
                      <LockOpen className="size-4" aria-hidden />
                      {publishedSelected.length > 0 ? `Unpublish selected (${publishedSelected.length})` : "Unpublish class"}
                    </Button>
                  }
                  onConfirm={async () => {
                    setResult(null);
                    const r = await runAction(() => unpublishReports(summary.classRoomId, summary.termId, publishedSelected), {
                      success: (x) => `${x.unpublished} ${x.unpublished === 1 ? "report" : "reports"} reopened for corrections`,
                    });
                    if (r) setSelected(new Set());
                    return r !== null;
                  }}
                >
                  <p>Parents stop seeing these reports, and scores, comments and attendance can be edited again. Publish again when corrections are done.</p>
                </ConfirmDialog>
              )}
            </>
          )}
      </div>

      <div className="overflow-x-auto rounded-card border border-border bg-surface shadow-low">
        <table className="w-full min-w-[44rem] text-left text-sm">
          <caption className="sr-only">
            {summary.classRoom} reports, {summary.term}
          </caption>
          <thead className="bg-surface-2 text-xs uppercase tracking-wide text-muted">
            <tr>
              {isAdmin && (
                <th scope="col" className="w-10 px-3 py-2.5">
                  <input
                    type="checkbox"
                    className="size-4 accent-[var(--brand)]"
                    aria-label="Select all"
                    checked={selected.size > 0 && selected.size === summary.rows.length}
                    onChange={(e) => setSelected(e.target.checked ? new Set(summary.rows.map((r) => r.studentId)) : new Set())}
                  />
                </th>
              )}
              <th scope="col" className="px-3 py-2.5 font-medium">Student</th>
              <th scope="col" className="px-3 py-2.5 text-center font-medium">Subjects</th>
              <th scope="col" className="px-3 py-2.5 text-center font-medium">Average</th>
              <th scope="col" className="px-3 py-2.5 text-center font-medium">Position</th>
              <th scope="col" className="px-3 py-2.5 text-center font-medium">Attendance</th>
              <th scope="col" className="px-3 py-2.5 text-center font-medium" title="Form teacher's comment">FT</th>
              <th scope="col" className="px-3 py-2.5 text-center font-medium" title="Principal's comment">P</th>
              <th scope="col" className="px-3 py-2.5 font-medium">Status</th>
              <th scope="col" className="w-8 px-2 py-2.5"><span className="sr-only">Open</span></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {summary.rows.map((r) => {
              const partial = r.subjectsScored < summary.subjectsOffered;
              return (
                <tr key={r.studentId} className={cn("transition-colors hover:bg-surface-2/50", selected.has(r.studentId) && "bg-brand/5")}>
                  {isAdmin && (
                    <td className="px-3 py-2.5">
                      <input type="checkbox" className="size-4 accent-[var(--brand)]" aria-label={`Select ${r.name}`} checked={selected.has(r.studentId)} onChange={() => toggle(r.studentId)} />
                    </td>
                  )}
                  <th scope="row" className="px-3 py-2.5 font-normal">
                    <Link href={href(r.studentId)} className="font-medium hover:text-brand hover:underline">
                      {r.name}
                    </Link>
                    <span className="block font-mono text-xs text-muted">{r.admissionNumber}</span>
                  </th>
                  <td className={cn("tabular px-3 py-2.5 text-center", partial && "font-medium text-warning")} title={partial ? "Not every subject has scores yet" : undefined}>
                    {r.subjectsScored}/{summary.subjectsOffered}
                    {partial && <span className="sr-only"> (incomplete)</span>}
                  </td>
                  <td className="tabular px-3 py-2.5 text-center">{formatAverage(r.average)}</td>
                  <td className="tabular px-3 py-2.5 text-center">{r.position ? ordinal(r.position) : "—"}</td>
                  <td className={cn("tabular px-3 py-2.5 text-center", r.daysPresent === null && r.subjectsScored > 0 && "text-warning")}>{attendanceText(r, summary.daysOpened)}</td>
                  <td className="px-3 py-2.5 text-center">
                    <Tick on={r.hasFormTeacherComment} label="Form teacher's comment" />
                  </td>
                  <td className="px-3 py-2.5 text-center">
                    <Tick on={r.hasPrincipalComment} label="Principal's comment" />
                  </td>
                  <td className="px-3 py-2.5">{r.status === "Published" ? <Badge tone="success">Published</Badge> : <Badge>Draft</Badge>}</td>
                  <td className="px-2 py-2.5">
                    <Link href={href(r.studentId)} aria-label={`Open ${r.name}'s report`} className="inline-flex size-8 items-center justify-center rounded-input text-muted hover:bg-surface-2 hover:text-text">
                      <ChevronRight className="size-4" aria-hidden />
                    </Link>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
