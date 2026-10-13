import type { components } from "./schema";

type Schemas = components["schemas"];

export type AuthResponse = Schemas["AuthResponse"];
export type MeResponse = Schemas["MeResponse"];
export type AuditLogEntry = Schemas["AuditLogEntry"];
export type AuditFilters = Schemas["AuditFilters"];
export type AuditAction = Schemas["AuditAction"];
export type Role = Schemas["Role"];
export type PagedAuditLog = Schemas["PagedResultOfAuditLogEntry"];
export type LoginCommand = Schemas["LoginCommand"];
export type ChangePasswordCommand = Schemas["ChangePasswordCommand"];

export type SessionDto = Schemas["SessionDto"];
export type TermDto = Schemas["TermDto"];
export type TermNumber = Schemas["TermNumber"];
export type ClassLevelDto = Schemas["ClassLevelDto"];
export type ClassRoomDto = Schemas["ClassRoomDto"];
export type ClassSubjectDto = Schemas["ClassSubjectDto"];
export type ClassSubjectAssignment = Schemas["ClassSubjectAssignment"];
export type SubjectDto = Schemas["SubjectDto"];
export type SchoolSection = Schemas["SchoolSection"];
