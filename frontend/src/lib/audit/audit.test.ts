import { describe, expect, it } from "vitest";
import { diffRows } from "./changes";
import { auditHref, pageCount, parseAuditSearch, toApiQuery } from "./query";
import { activeHref, consoleNav, filterNav } from "@/components/shell/nav";

describe("audit query", () => {
  it("parses URL params, dropping junk", () => {
    expect(parseAuditSearch({ entityType: " Student ", action: "", from: "2026-10-01", to: "13/10/2026", page: "-3" })).toEqual({
      entityType: "Student",
      action: undefined,
      entityId: undefined,
      from: "2026-10-01",
      to: undefined,
      page: 1,
    });
  });

  it("sends school-day bounds with an exclusive `to`", () => {
    const q = toApiQuery(parseAuditSearch({ from: "2026-10-01", to: "2026-10-31", page: "2" }));
    expect(q).toMatchObject({ from: "2026-10-01T00:00:00+01:00", to: "2026-11-01T00:00:00+01:00", page: 2, pageSize: 25 });
  });

  it("builds shareable links and omits page 1", () => {
    const s = parseAuditSearch({ action: "Modified", page: "3" });
    expect(auditHref(s)).toBe("/admin/audit?action=Modified&page=3");
    expect(auditHref(s, { page: 1 })).toBe("/admin/audit?action=Modified");
    expect(pageCount(0, 25)).toBe(1);
    expect(pageCount(51, 25)).toBe(3);
  });
});

describe("audit diff", () => {
  it("Added shows after-values only, with secrets redacted", () => {
    expect(diffRows("Added", { Name: "JSS 1A", PasswordHash: "***", Capacity: 30 })).toEqual([
      { field: "Name", before: { kind: "absent" }, after: { kind: "value", text: "JSS 1A" }, changed: true },
      { field: "PasswordHash", before: { kind: "absent" }, after: { kind: "redacted" }, changed: true },
      { field: "Capacity", before: { kind: "absent" }, after: { kind: "value", text: "30" }, changed: true },
    ]);
  });

  it("Deleted shows before-values only", () => {
    expect(diffRows("Deleted", { Name: "Old" })[0]).toMatchObject({ before: { kind: "value", text: "Old" }, after: { kind: "absent" } });
  });

  it("Modified shows from → to, including nulls", () => {
    expect(diffRows("Modified", { Email: { from: null, to: "a@b.c" }, Secret: { from: "***", to: "***" } })).toEqual([
      { field: "Email", before: { kind: "null" }, after: { kind: "value", text: "a@b.c" }, changed: true },
      { field: "Secret", before: { kind: "redacted" }, after: { kind: "redacted" }, changed: false },
    ]);
  });

  it("Modified secrets masked as a whole are redacted on both sides", () => {
    expect(diffRows("Modified", { ConcurrencyStamp: "***" })[0]).toMatchObject({ before: { kind: "redacted" }, after: { kind: "redacted" } });
  });

  it("tolerates missing or odd change payloads", () => {
    expect(diffRows("Modified", null)).toEqual([]);
    expect(diffRows("Modified", [1, 2])).toEqual([]);
  });
});

describe("console navigation", () => {
  const labels = (roles: string[]) => filterNav(consoleNav, roles).flatMap((g) => g.items.map((i) => i.label));

  it("shows the audit log to admins only", () => {
    expect(labels(["Admin"])).toContain("Audit log");
    expect(labels(["Teacher"])).not.toContain("Audit log");
    expect(labels(["Bursar"])).not.toContain("Audit log");
  });

  it("gives teachers their working set", () => {
    expect(labels(["Teacher"])).toEqual(["Dashboard", "Students", "My classes", "Sessions & terms", "Score entry", "Report comments", "Attendance"]);
  });

  it("highlights the most specific match", () => {
    expect(activeHref("/admin/audit", ["/admin", "/admin/audit"])).toBe("/admin/audit");
    expect(activeHref("/admin", ["/admin", "/admin/audit"])).toBe("/admin");
  });
});

describe("audit user filter", () => {
  it("accepts only a UUID userId and keeps it in links", () => {
    const id = "8d1f4c1e-0000-4000-8000-000000000001";
    expect(parseAuditSearch({ userId: "drop table" }).userId).toBeUndefined();
    const s = parseAuditSearch({ userId: id });
    expect(toApiQuery(s).userId).toBe(id);
    expect(auditHref(s)).toBe(`/admin/audit?userId=${id}`);
  });
});
