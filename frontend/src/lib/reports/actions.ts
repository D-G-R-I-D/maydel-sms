"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { ClassReportSummary, PublishResult, ReportCard, UnpublishResult } from "@/lib/api/types";

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

export async function publishClass(classRoomId: string, termId: string): Promise<Loaded<PublishResult>> {
  return done(await load((api) => api.POST("/api/reports/publish", { body: { classRoomId, termId } })));
}

export async function unpublishReports(classRoomId: string, termId: string, studentIds?: string[]): Promise<Loaded<UnpublishResult>> {
  return done(await load((api) => api.POST("/api/reports/unpublish", { body: { classRoomId, termId, studentIds: studentIds?.length ? studentIds : null } })));
}
