"use client";

import { Fragment, useState } from "react";
import Link from "next/link";
import { ChevronDown, ChevronsDownUp, ChevronsUpDown, Copy, Filter, Lock, Minus, Pencil, Plus, Trash2 } from "lucide-react";
import type { AuditLogEntry } from "@/lib/api/types";
import { diffRows, type DiffValue } from "@/lib/audit/changes";
import { formatTimestamp } from "@/lib/audit/query";
import { Badge, buttonClasses, cn } from "@/components/ui/primitives";
import { toast } from "@/components/ui/toast";

function ActionBadge({ action }: { action: string }) {
  if (action === "Added")
    return (
      <Badge tone="success">
        <Plus className="size-3" aria-hidden />
        Added
      </Badge>
    );
  if (action === "Deleted")
    return (
      <Badge tone="danger">
        <Trash2 className="size-3" aria-hidden />
        Deleted
      </Badge>
    );
  return (
    <Badge tone="brand">
      <Pencil className="size-3" aria-hidden />
      {action}
    </Badge>
  );
}

function Value({ value }: { value: DiffValue }) {
  switch (value.kind) {
    case "absent":
      return (
        <span className="text-muted">
          <Minus className="inline size-3.5" aria-hidden />
          <span className="sr-only">none</span>
        </span>
      );
    case "redacted":
      return (
        <Badge tone="neutral">
          <Lock className="size-3" aria-hidden />
          redacted
        </Badge>
      );
    case "null":
      return <span className="italic text-muted">empty</span>;
    default:
      return <span className="break-all font-mono text-xs">{value.text}</span>;
  }
}

function ChangesDiff({ entry }: { entry: AuditLogEntry }) {
  const rows = diffRows(entry.action, entry.changes);
  if (rows.length === 0) return <p className="text-sm text-muted">No field-level details were recorded for this change.</p>;
  return (
    <div className="overflow-x-auto rounded-input border border-border">
      <table className="w-full text-left text-sm">
        <caption className="sr-only">Field changes for {entry.entityType} {entry.entityId}</caption>
        <thead className="bg-surface-2 text-xs uppercase tracking-wide text-muted">
          <tr>
            <th scope="col" className="px-3 py-2 font-medium">Field</th>
            <th scope="col" className="px-3 py-2 font-medium">Before</th>
            <th scope="col" className="px-3 py-2 font-medium">After</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {rows.map((row) => (
            <tr key={row.field} className={cn(!row.changed && "text-muted")}>
              <th scope="row" className="px-3 py-2 align-top font-medium">{row.field}</th>
              <td className={cn("px-3 py-2 align-top", row.changed && row.before.kind !== "absent" && "bg-danger/5")}>
                <Value value={row.before} />
              </td>
              <td className={cn("px-3 py-2 align-top", row.changed && row.after.kind !== "absent" && "bg-success/5")}>
                <Value value={row.after} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

async function copy(text: string, what: string) {
  try {
    await navigator.clipboard.writeText(text);
    toast.success(`${what} copied`, { description: text, duration: 2500 });
  } catch {
    toast.error("Couldn't copy", { description: "Your browser blocked clipboard access." });
  }
}

/** Expanded panel: quick actions for the record, then the field diff. */
function EntryDetails({ entry }: { entry: AuditLogEntry }) {
  const only = new URLSearchParams({ entityType: entry.entityType, ...(entry.entityId ? { entityId: entry.entityId } : {}) });
  return (
    <div className="animate-fade space-y-3">
      <div className="flex flex-wrap gap-2">
        {entry.entityId && (
          <>
            <button type="button" onClick={() => copy(entry.entityId!, "Record ID")} className={buttonClasses("secondary", "sm")}>
              <Copy className="size-3.5" aria-hidden />
              Copy ID
            </button>
            <Link href={`/admin/audit?${only}`} scroll={false} className={buttonClasses("secondary", "sm")}>
              <Filter className="size-3.5" aria-hidden />
              History of this record
            </Link>
          </>
        )}
        {entry.userId && (
          <Link href={`/admin/audit?userId=${entry.userId}`} scroll={false} className={buttonClasses("ghost", "sm")}>
            Changes by {entry.userName ?? "this user"}
          </Link>
        )}
      </div>
      <ChangesDiff entry={entry} />
    </div>
  );
}

function ExpandButton({ open, onClick, controls, label }: { open: boolean; onClick: () => void; controls: string; label: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-expanded={open}
      aria-controls={controls}
      aria-label={label}
      className="inline-flex size-10 items-center justify-center rounded-input text-muted hover:bg-surface-2 hover:text-text"
    >
      <ChevronDown className={cn("size-4 transition-transform duration-150", open && "rotate-180")} aria-hidden />
    </button>
  );
}

/** Table from md up; stacked cards on phones. Each entry expands to a before/after diff. */
export function AuditTable({ entries }: { entries: AuditLogEntry[] }) {
  const [open, setOpen] = useState<ReadonlySet<number>>(new Set());
  const toggle = (id: number) =>
    setOpen((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const allOpen = entries.length > 0 && entries.every((e) => open.has(e.id));

  return (
    <>
      <div className="flex items-center justify-end border-b border-border px-3 py-2">
        <button
          type="button"
          onClick={() => setOpen(allOpen ? new Set() : new Set(entries.map((e) => e.id)))}
          className={buttonClasses("ghost", "sm")}
          aria-pressed={allOpen}
        >
          {allOpen ? <ChevronsDownUp className="size-4" aria-hidden /> : <ChevronsUpDown className="size-4" aria-hidden />}
          {allOpen ? "Collapse all" : "Expand all"}
        </button>
      </div>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full text-left text-sm">
          <caption className="sr-only">Audit log entries, newest first</caption>
          <thead className="sticky top-0 bg-surface-2 text-xs uppercase tracking-wide text-muted">
            <tr>
              <th scope="col" className="w-12 px-2 py-3"><span className="sr-only">Details</span></th>
              <th scope="col" className="px-3 py-3 font-medium">When</th>
              <th scope="col" className="px-3 py-3 font-medium">User</th>
              <th scope="col" className="px-3 py-3 font-medium">Action</th>
              <th scope="col" className="px-3 py-3 font-medium">Record</th>
              <th scope="col" className="px-3 py-3 font-medium">IP address</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {entries.map((e) => {
              const isOpen = open.has(e.id);
              const panelId = `audit-${e.id}`;
              return (
                <Fragment key={e.id}>
                  <tr className={cn("hover:bg-surface-2/50", isOpen && "bg-surface-2/50")}>
                    <td className="px-2 py-1">
                      <ExpandButton open={isOpen} onClick={() => toggle(e.id)} controls={panelId} label={`${isOpen ? "Hide" : "Show"} changes for ${e.entityType} ${e.entityId ?? ""}`} />
                    </td>
                    <td className="tabular whitespace-nowrap px-3 py-2">
                      <time dateTime={e.timestamp}>{formatTimestamp(e.timestamp)}</time>
                    </td>
                    <td className="px-3 py-2">{e.userName ?? <span className="text-muted">System</span>}</td>
                    <td className="px-3 py-2"><ActionBadge action={e.action} /></td>
                    <td className="px-3 py-2">
                      <span className="font-medium">{e.entityType}</span>
                      {e.entityId && <span className="block max-w-56 truncate font-mono text-xs text-muted" title={e.entityId}>{e.entityId}</span>}
                    </td>
                    <td className="tabular px-3 py-2 font-mono text-xs">{e.ipAddress ?? <span className="text-muted">—</span>}</td>
                  </tr>
                  {isOpen && (
                    <tr id={panelId}>
                      <td colSpan={6} className="bg-surface-2/30 px-4 py-3">
                        <EntryDetails entry={e} />
                      </td>
                    </tr>
                  )}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      </div>

      <ul className="divide-y divide-border md:hidden">
        {entries.map((e) => {
          const isOpen = open.has(e.id);
          const panelId = `audit-m-${e.id}`;
          return (
            <li key={e.id} className="px-4 py-3">
              <div className="flex items-start gap-2">
                <div className="min-w-0 flex-1 space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <ActionBadge action={e.action} />
                    <span className="font-medium">{e.entityType}</span>
                  </div>
                  <p className="text-sm">{e.userName ?? "System"}</p>
                  <p className="tabular text-xs text-muted">
                    <time dateTime={e.timestamp}>{formatTimestamp(e.timestamp)}</time>
                    {e.ipAddress && <> · <span className="font-mono">{e.ipAddress}</span></>}
                  </p>
                  {e.entityId && <p className="truncate font-mono text-xs text-muted">{e.entityId}</p>}
                </div>
                <ExpandButton open={isOpen} onClick={() => toggle(e.id)} controls={panelId} label={`${isOpen ? "Hide" : "Show"} changes`} />
              </div>
              {isOpen && (
                <div id={panelId} className="mt-3">
                  <EntryDetails entry={e} />
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </>
  );
}
