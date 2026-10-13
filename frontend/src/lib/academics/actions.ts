"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type { ClassRoomDto, ClassSubjectAssignment, ClassSubjectDto, SessionDto, SubjectDto, TermDto, TermNumber } from "@/lib/api/types";

/**
 * School-setup mutations (M1.1). The API is the authority on who may do what; these only forward
 * the call with the signed-in user's bearer and refresh the academics pages on success.
 */

type Dates = { startsOn: string; endsOn: string; midTermStartsOn: string | null; midTermEndsOn: string | null };

function done<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) {
    revalidatePath("/admin/academics", "layout");
    revalidatePath("/admin", "layout"); // header chip shows the current term
  }
  return result;
}

export async function createSession(body: { name: string; startsOn: string; endsOn: string }): Promise<Loaded<SessionDto>> {
  return done(await load((api) => api.POST("/api/academics/sessions", { body })));
}

export async function updateSession(id: string, body: { name: string; startsOn: string; endsOn: string }): Promise<Loaded<SessionDto>> {
  return done(await load((api) => api.PUT("/api/academics/sessions/{id}", { params: { path: { id } }, body })));
}

export async function addTerm(sessionId: string, body: Dates & { number: TermNumber }): Promise<Loaded<TermDto>> {
  return done(await load((api) => api.POST("/api/academics/sessions/{id}/terms", { params: { path: { id: sessionId } }, body })));
}

export async function updateTerm(id: string, body: Dates): Promise<Loaded<TermDto>> {
  return done(await load((api) => api.PUT("/api/academics/terms/{id}", { params: { path: { id } }, body })));
}

export async function makeTermCurrent(id: string): Promise<Loaded<TermDto>> {
  return done(await load((api) => api.POST("/api/academics/terms/{id}/make-current", { params: { path: { id } } })));
}

export async function createClassroom(body: { classLevelId: string; arm: string; capacity: number; formTeacherId: string | null }): Promise<Loaded<ClassRoomDto>> {
  return done(await load((api) => api.POST("/api/academics/classrooms", { body })));
}

export async function updateClassroom(id: string, body: { arm: string; capacity: number; formTeacherId: string | null }): Promise<Loaded<ClassRoomDto>> {
  return done(await load((api) => api.PUT("/api/academics/classrooms/{id}", { params: { path: { id } }, body })));
}

export async function saveClassSubjects(classroomId: string, subjects: ClassSubjectAssignment[]): Promise<Loaded<ClassSubjectDto[]>> {
  return done(await load((api) => api.PUT("/api/academics/classrooms/{id}/subjects", { params: { path: { id: classroomId } }, body: { subjects } })));
}

export async function createSubject(body: { name: string; code: string; isActive: boolean }): Promise<Loaded<SubjectDto>> {
  return done(await load((api) => api.POST("/api/academics/subjects", { body })));
}

export async function updateSubject(id: string, body: { name: string; code: string; isActive: boolean }): Promise<Loaded<SubjectDto>> {
  return done(await load((api) => api.PUT("/api/academics/subjects/{id}", { params: { path: { id } }, body })));
}
