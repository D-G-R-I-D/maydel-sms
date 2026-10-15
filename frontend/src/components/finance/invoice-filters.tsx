"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, useTransition, type ReactNode } from "react";
import { FileStack, Search } from "lucide-react";
import { problemMessage } from "@/lib/api/problem";
import type { ClassRoomDto } from "@/lib/api/types";
import { generateInvoices } from "@/lib/finance/actions";
import { INVOICE_STATUSES, invoicesHref, type InvoiceSearch } from "@/lib/finance/invoices";
import { generateToast, invoiceStatusLabel } from "@/lib/finance/rules";
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
      <Card className="grid gap-3 p-3 sm:grid-cols-[1fr_auto] lg:grid-cols-[1fr_14rem_12rem]">
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
                  success: generateToast,
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
