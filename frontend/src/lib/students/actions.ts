"use server";

import { revalidatePath } from "next/cache";
import { load, type Loaded } from "@/lib/api/server";
import type {
  GuardianAdded,
  GuardianDetail,
  GuardianListItem,
  GuardianLoginIssued,
  GuardianRelationship,
  IssuedCredentials,
  Sex,
  StudentDetail,
  StudentStatus,
} from "@/lib/api/types";

/**
 * Student & guardian mutations (M1.1 part 3). Login responses carry a one-time temporary password,
 * returned straight to the browser for the credential slip; nothing here logs or stores it.
 */

export type StudentBody = {
  firstName: string;
  middleName: string | null;
  lastName: string;
  dateOfBirth: string | null;
  sex: Sex | null;
  stateOfOrigin: string | null;
  lga: string | null;
  homeAddress: string | null;
  house: string | null;
  medicalNotes: string | null;
  classRoomId: string | null;
  admittedOn: string;
};

type GuardianDetails = { fullName: string; phone: string; email: string | null; occupation: string | null; address: string | null };
type GuardianLink = { relationship: GuardianRelationship; isPrimaryContact: boolean; canPickUp: boolean };

function done<T>(result: Loaded<T>): Loaded<T> {
  if (result.ok) {
    revalidatePath("/admin/students", "layout");
    revalidatePath("/admin/guardians", "layout");
    revalidatePath("/admin/academics", "layout"); // class student counts
  }
  return result;
}

export async function admitStudent(body: StudentBody): Promise<Loaded<StudentDetail>> {
  return done(await load((api) => api.POST("/api/students", { body })));
}

export async function updateStudent(id: string, body: StudentBody): Promise<Loaded<StudentDetail>> {
  return done(await load((api) => api.PUT("/api/students/{id}", { params: { path: { id } }, body })));
}

export async function changeStudentStatus(id: string, status: StudentStatus): Promise<Loaded<StudentDetail>> {
  return done(await load((api) => api.POST("/api/students/{id}/status", { params: { path: { id } }, body: { status } })));
}

export async function issueStudentLogin(id: string): Promise<Loaded<IssuedCredentials>> {
  return done(await load((api) => api.POST("/api/students/{id}/login", { params: { path: { id } } })));
}

export async function addGuardian(studentId: string, body: GuardianDetails & GuardianLink): Promise<Loaded<GuardianAdded>> {
  return done(await load((api) => api.POST("/api/students/{id}/guardians", { params: { path: { id: studentId } }, body })));
}

export async function updateGuardianLink(studentId: string, guardianId: string, body: GuardianLink): Promise<Loaded<StudentDetail>> {
  return done(await load((api) => api.PUT("/api/students/{id}/guardians/{guardianId}", { params: { path: { id: studentId, guardianId } }, body })));
}

export async function removeGuardian(studentId: string, guardianId: string): Promise<Loaded<true>> {
  const result = await load(async (api) => {
    const r = await api.DELETE("/api/students/{id}/guardians/{guardianId}", { params: { path: { id: studentId, guardianId } } });
    return { ...r, data: r.response.ok ? (true as const) : undefined };
  });
  return done(result);
}

export async function updateGuardian(id: string, body: GuardianDetails): Promise<Loaded<GuardianDetail>> {
  return done(await load((api) => api.PUT("/api/guardians/{id}", { params: { path: { id } }, body })));
}

export async function issueGuardianLogin(id: string): Promise<Loaded<GuardianLoginIssued>> {
  return done(await load((api) => api.POST("/api/guardians/{id}/login", { params: { path: { id } } })));
}

/** Read-only: "Existing guardian found" hint while typing a phone in Add guardian. */
export async function findGuardians(search: string): Promise<Loaded<GuardianListItem[]>> {
  const q = search.trim();
  if (q.length < 4) return { ok: true, data: [] };
  const result = await load((api) => api.GET("/api/guardians", { params: { query: { search: q, page: 1, pageSize: 3 } } }));
  return result.ok ? { ok: true, data: result.data.items } : result;
}
