/** The school's clock: Africa/Lagos is UTC+1 all year (no DST). Date filters are school days. */
const SCHOOL_OFFSET = "+01:00";
export const AUDIT_PAGE_SIZE = 25;

export type AuditSearch = {
  entityType?: string;
  action?: string;
  entityId?: string;
  userId?: string;
  from?: string; // yyyy-mm-dd, inclusive
  to?: string; // yyyy-mm-dd, inclusive (converted to an exclusive bound for the API)
  page: number;
};

type Params = Record<string, string | string[] | undefined>;

const isoDate = /^\d{4}-\d{2}-\d{2}$/;
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function first(v: string | string[] | undefined): string | undefined {
  const value = Array.isArray(v) ? v[0] : v;
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

function validDate(v: string | undefined): string | undefined {
  if (!v || !isoDate.test(v)) return undefined;
  return Number.isNaN(Date.parse(`${v}T00:00:00Z`)) ? undefined : v;
}

export function parseAuditSearch(params: Params): AuditSearch {
  const page = Number.parseInt(first(params.page) ?? "1", 10);
  return {
    entityType: first(params.entityType),
    action: first(params.action),
    entityId: first(params.entityId),
    userId: uuid.test(first(params.userId) ?? "") ? first(params.userId) : undefined,
    from: validDate(first(params.from)),
    to: validDate(first(params.to)),
    page: Number.isFinite(page) && page > 0 ? page : 1,
  };
}

function nextDay(date: string): string {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + 1);
  return d.toISOString().slice(0, 10);
}

/** API query: `from` inclusive at school midnight, `to` exclusive at the following school midnight. */
export function toApiQuery(search: AuditSearch) {
  return {
    entityType: search.entityType,
    action: search.action,
    entityId: search.entityId,
    userId: search.userId,
    from: search.from ? `${search.from}T00:00:00${SCHOOL_OFFSET}` : undefined,
    to: search.to ? `${nextDay(search.to)}T00:00:00${SCHOOL_OFFSET}` : undefined,
    page: search.page,
    pageSize: AUDIT_PAGE_SIZE,
  };
}

export function hasFilters(search: AuditSearch): boolean {
  return Boolean(search.entityType || search.action || search.entityId || search.userId || search.from || search.to);
}

/** URL for the audit page with these filters (page 1 is omitted). */
export function auditHref(search: AuditSearch, overrides: Partial<AuditSearch> = {}): string {
  const merged = { ...search, ...overrides };
  const qs = new URLSearchParams();
  for (const key of ["entityType", "action", "entityId", "userId", "from", "to"] as const) {
    const value = merged[key];
    if (value) qs.set(key, value);
  }
  if (merged.page > 1) qs.set("page", String(merged.page));
  const s = qs.toString();
  return s ? `/admin/audit?${s}` : "/admin/audit";
}

export function pageCount(totalCount: number, pageSize: number): number {
  return Math.max(1, Math.ceil(totalCount / Math.max(1, pageSize)));
}

const timestampFormat = new Intl.DateTimeFormat("en-NG", {
  timeZone: "Africa/Lagos",
  day: "2-digit",
  month: "short",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  hour12: false,
});

export function formatTimestamp(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : timestampFormat.format(d);
}
