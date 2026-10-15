"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { ClassReportSummary, PublishResult, ReportCard, ReportEmailStatus, UnpublishResult } from "@/lib/api/types";

/** Report mutations (E5.3–E5.5). The API enforces who may do what and the published lock. */

function done<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) {
    revalidatePath("/admin/reports", "layout");
    revalidatePath("/admin/assessment", "layout"); // locked rows in the score grid
  }
  return result;
}

export async function saveAttendance(body: {
  classRoomId: string;
  termId: string;
  daysOpened: number;
  entries: { studentId: string; daysPresent: number }[];
}): Promise<Loaded<ClassReportSummary>> {
  return done(await load((api) => api.PUT("/api/reports/attendance", { body })));
}

export async function saveFormTeacherComment(studentId: string, termId: string, comment: string): Promise<Loaded<ReportCard>> {
  return done(
    await load((api) =>
      api.PUT("/api/reports/{studentId}/{termId}/form-teacher-comment", { params: { path: { studentId, termId } }, body: { comment: comment.trim() || null } }),
    ),
  );
}

export async function savePrincipalComment(studentId: string, termId: string, comment: string): Promise<Loaded<ReportCard>> {
  return done(
    await load((api) =>
      api.PUT("/api/reports/{studentId}/{termId}/principal-comment", { params: { path: { studentId, termId } }, body: { comment: comment.trim() || null } }),
    ),
  );
}

export async function publishClass(classRoomId: string, termId: string, notifyParents: boolean): Promise<Loaded<PublishResult>> {
  return done(await load((api) => api.POST("/api/reports/publish", { body: { classRoomId, termId, notifyParents } })));
}

export async function unpublishReports(classRoomId: string, termId: string, studentIds?: string[]): Promise<Loaded<UnpublishResult>> {
  return done(await load((api) => api.POST("/api/reports/unpublish", { body: { classRoomId, termId, studentIds: studentIds?.length ? studentIds : null } })));
}

export async function saveBehaviour(
  studentId: string,
  termId: string,
  body: { ratings: { traitId: string; rating: string | null }[]; clubs: { name: string; rating: string | null }[] },
): Promise<Loaded<ReportCard>> {
  return done(await load((api) => api.PUT("/api/reports/{studentId}/{termId}/behaviour", { params: { path: { studentId, termId } }, body })));
}

/** E12.3: report-card emails to parents for a class and term (admin), newest first. Read-only; used for polling. */
export async function loadReportEmails(classRoomId: string, termId: string): Promise<Loaded<ReportEmailStatus[]>> {
  return load((api) => api.GET("/api/reports/emails", { params: { query: { classRoomId, termId } } }));
}

/** Queues a Failed email again (422 email.not_failed otherwise). */
export async function retryReportEmail(emailId: string): Promise<Loaded<true>> {
  return load(async (api) => {
    const r = await api.POST("/api/reports/emails/{emailId}/retry", { params: { path: { emailId } } });
    return { ...r, data: r.response.ok ? (true as const) : undefined };
  });
}
