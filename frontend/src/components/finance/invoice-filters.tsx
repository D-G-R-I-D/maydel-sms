"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition, type ReactNode } from "react";
import { FileStack, Search, Send } from "lucide-react";
import { problemMessage } from "@/lib/api/problem";
import type { ClassRoomDto } from "@/lib/api/types";
import { generateInvoices, publishInvoices } from "@/lib/finance/actions";
import { INVOICE_STATUSES, invoicesHref, type InvoiceSearch } from "@/lib/finance/invoices";
import { generateToast, invoiceStatusLabel, publishInvoicesToast } from "@/lib/finance/rules";
import { groupBySection } from "@/lib/validation/academics";
import { runAction } from "@/lib/client/action";
import { Button, Card, Input, Select, cn } from "@/components/ui/primitives";
import { CancelButton, DialogActions, FormDialog } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";

/** Debounced search + class + status in the URL; results dim while loading (same as the students list). */
export function InvoiceFilters({ search, classrooms, children }: { search: InvoiceSearch; classrooms: ClassRoomDto[]; children: ReactNode }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [text, setText] = useState(search.search ?? "");
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);

  const go = (overrides: Partial<InvoiceSearch>) => startTransition(() => router.replace(invoicesHref(search, { ...overrides, page: 1 }), { scroll: false }));

  return (
    <>
      <Card className="grid gap-3 p-3 sm:grid-cols-2 lg:grid-cols-[1fr_13rem_11rem_11rem_auto]">
        <form
          role="search"
          className="relative sm:col-span-2 lg:col-span-1"
          onSubmit={(e) => {
            e.preventDefault();
            clearTimeout(timer.current);
            go({ search: text.trim() || undefined });
          }}
        >
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
          <Input
            value={text}
            onChange={(e) => {
              const v = e.target.value;
              setText(v);
              clearTimeout(timer.current);
              timer.current = setTimeout(() => go({ search: v.trim() || undefined }), 400);
            }}
            placeholder="Search name, admission or invoice number"
            aria-label="Search invoices"
            className="pl-9"
          />
        </form>
        <Select aria-label="Class" value={search.classRoomId ?? ""} onChange={(e) => go({ classRoomId: e.target.value || undefined })}>
          <option value="">All classes</option>
          {groupBySection(classrooms).map((g) => (
            <optgroup key={g.section} label={g.label}>
              {g.rows.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </optgroup>
          ))}
        </Select>
        <Select aria-label="Status" value={search.status ?? ""} onChange={(e) => go({ status: (e.target.value || undefined) as InvoiceSearch["status"] })}>
          <option value="">All statuses</option>
          {INVOICE_STATUSES.map((s) => (
            <option key={s} value={s}>
              {invoiceStatusLabel[s]}
            </option>
          ))}
        </Select>
        <Select
          aria-label="Draft or published"
          value={search.published === undefined ? "" : String(search.published)}
          onChange={(e) => go({ published: e.target.value === "" ? undefined : e.target.value === "true" })}
        >
          <option value="">Drafts and published</option>
          <option value="false">Drafts only</option>
          <option value="true">Published only</option>
        </Select>
        <label htmlFor="inv-new-intake" className="flex min-h-11 cursor-pointer items-center gap-2 text-sm whitespace-nowrap">
          <input id="inv-new-intake" type="checkbox" className="size-4 accent-[var(--brand)]" checked={Boolean(search.newIntake)} onChange={(e) => go({ newIntake: e.target.checked || undefined })} />
          New intake
        </label>
      </Card>
      <Card className="relative mt-4 overflow-hidden" aria-busy={pending}>
        <div className={cn("pointer-events-none absolute inset-x-0 top-0 z-10 h-0.5 overflow-hidden", !pending && "hidden")} aria-hidden>
          <div className="animate-progress h-full w-1/3 bg-brand" />
        </div>
        <div className={cn("transition-opacity duration-200", pending && "opacity-50")}>{children}</div>
      </Card>
    </>
  );
}

/**
 * E10.7: publish draft invoices for the term (optionally one level). Parents then see them in the
 * portal and, with the box ticked, get the invoice PDF by email. Running it again only publishes new drafts.
 */
export function PublishInvoicesButton({ termId, termLabel, levels, drafts }: { termId: string; termLabel: string; levels: { id: string; name: string }[]; drafts: number }) {
  const [level, setLevel] = useState("");
  const [notify, setNotify] = useState(true);
  const [busy, setBusy] = useState(false);
  return (
    <FormDialog
      title="Publish invoices"
      description={termLabel}
      trigger={
        <Button variant={drafts > 0 ? "primary" : "secondary"}>
          <Send className="size-4" aria-hidden />
          Publish invoices{drafts > 0 ? ` (${drafts})` : ""}
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4">
          <p className="text-sm text-muted">
            {drafts > 0 ? `Publish ${drafts} draft ${drafts === 1 ? "invoice" : "invoices"}?` : "There are no drafts for this term right now."} Published invoices appear in the
            parents&apos; portal and can be paid online.
          </p>
          <div className="space-y-1.5">
            <label htmlFor="pub-level" className="block text-sm font-medium">
              Which
            </label>
            <Select id="pub-level" value={level} onChange={(e) => setLevel(e.target.value)}>
              <option value="">Every class level</option>
              {levels.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name} only
                </option>
              ))}
            </Select>
          </div>
          <label htmlFor="pub-notify" className="flex cursor-pointer items-start gap-2.5 rounded-input border border-border p-3 text-sm">
            <input id="pub-notify" type="checkbox" className="mt-0.5 size-4 shrink-0 accent-[var(--brand)]" checked={notify} onChange={(e) => setNotify(e.target.checked)} />
            <span>
              Email parents a copy
              <span className="block text-xs text-muted">
                {notify ? "Parents of these students will be emailed the invoice (guardians with an email address)." : "No emails will be sent."}
              </span>
            </span>
          </label>
          <DialogActions>
            <CancelButton />
            <Button
              loading={busy}
              loadingText="Publishing…"
              onClick={async () => {
                setBusy(true);
                const notified = notify;
                const r = await runAction(() => publishInvoices(termId, level || null, notified), {
                  success: (x) => publishInvoicesToast(x, notified),
                  onProblem: (p) => p.status > 0 && p.status < 500 && p.status !== 429 && toast.error("Couldn't publish invoices", { description: problemMessage(p) }),
                });
                setBusy(false);
                if (r) close();
              }}
            >
              <Send className="size-4" aria-hidden />
              Publish
            </Button>
          </DialogActions>
        </div>
      )}
    </FormDialog>
  );
}

/** Bills every active student (of one level, or all) for the term. Safe to run again: only new students are billed. */
export function GenerateInvoicesButton({ termId, termLabel, levels }: { termId: string; termLabel: string; levels: { id: string; name: string }[] }) {
  const [level, setLevel] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <FormDialog
      title="Generate invoices"
      description={termLabel}
      trigger={
        <Button>
          <FileStack className="size-4" aria-hidden />
          Generate invoices
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4">
          <p className="text-sm text-muted">
            Bills each active student: mandatory fees, the optional fees they take, minus any sibling discount. Students already invoiced for this term are skipped, so it&apos;s safe to run again after new admissions.
          </p>
          <div className="space-y-1.5">
            <label htmlFor="gen-level" className="block text-sm font-medium">
              Who
            </label>
            <Select id="gen-level" value={level} onChange={(e) => setLevel(e.target.value)}>
              <option value="">Every class level</option>
              {levels.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.name} only
                </option>
              ))}
            </Select>
          </div>
          <DialogActions>
            <CancelButton />
            <Button
              loading={busy}
              loadingText="Generating…"
              onClick={async () => {
                setBusy(true);
                const r = await runAction(() => generateInvoices(termId, level || null), {
                  success: (x) => (x.created > 0 ? `${generateToast(x)}. ${x.created} ${x.created === 1 ? "invoice is a draft" : "invoices are drafts"}: review, then Publish.` : generateToast(x)),
                  onProblem: (p) => p.status > 0 && p.status < 500 && p.status !== 429 && toast.error("Couldn't generate invoices", { description: problemMessage(p) }),
                });
                setBusy(false);
                if (r) close();
              }}
            >
              <FileStack className="size-4" aria-hidden />
              Generate
            </Button>
          </DialogActions>
        </div>
      )}
    </FormDialog>
  );
}
