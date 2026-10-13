import type { components } from "./schema";

type Schemas = components["schemas"];

export type AuthResponse = Schemas["AuthResponse"];
export type MeResponse = Schemas["MeResponse"];
export type AuditLogEntry = Schemas["AuditLogEntry"];
export type AuditFilters = Schemas["AuditFilters"];
export type PagedAuditLog = Schemas["PagedResultOfAuditLogEntry"];
export type LoginCommand = Schemas["LoginCommand"];
export type ChangePasswordCommand = Schemas["ChangePasswordCommand"];
