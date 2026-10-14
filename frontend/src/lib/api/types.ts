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

export type StaffListItem = Schemas["StaffListItem"];
export type StaffDetail = Schemas["StaffDetail"];
export type StaffAssignment = Schemas["StaffAssignment"];
export type StaffCreated = Schemas["StaffCreated"];
export type StaffLookupItem = Schemas["StaffLookupItem"];
export type IssuedCredentials = Schemas["IssuedCredentials"];
export type PagedStaff = Schemas["PagedResultOfStaffListItem"];

export type StudentListItem = Schemas["StudentListItem"];
export type StudentDetail = Schemas["StudentDetail"];
export type StudentStatus = Schemas["StudentStatus"];
export type Sex = NonNullable<Schemas["Sex"]>;
export type GuardianLink = Schemas["GuardianLink"];
export type GuardianAdded = Schemas["GuardianAdded"];
export type GuardianListItem = Schemas["GuardianListItem"];
export type GuardianDetail = Schemas["GuardianDetail"];
export type GuardianChild = Schemas["GuardianChild"];
export type GuardianRelationship = Schemas["GuardianRelationship"];
export type GuardianLoginIssued = Schemas["GuardianLoginIssued"];

export type AssessmentSettings = Schemas["AssessmentSettings"];
export type AssessmentComponent = Schemas["ComponentDto"];
export type ComponentInput = Schemas["ComponentInput"];
export type GradeBand = Schemas["GradeBandDto"];
export type GradableClass = Schemas["GradableClass"];
export type GradeSheet = Schemas["GradeSheet"];
export type GradeSheetRow = Schemas["GradeSheetRow"];
export type GradeSheetEntry = Schemas["GradeSheetEntry"];

export type ClassReportSummary = Schemas["ClassReportSummary"];
export type ClassReportRow = Schemas["ClassReportRow"];
export type ReportCard = Schemas["ReportCard"];
export type ReportSubjectLine = Schemas["ReportSubjectLine"];
export type PublishResult = Schemas["PublishResult"];
export type UnpublishResult = Schemas["UnpublishResult"];
