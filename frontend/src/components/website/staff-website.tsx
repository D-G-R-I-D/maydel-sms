"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { Pencil, Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { components } from "@/lib/api/schema";
import { runAction } from "@/lib/client/action";
import { saveStaffWebsite } from "@/lib/website/actions";
import { FormDialog } from "@/components/ui/dialog";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";

type Row = components["schemas"]["WebsiteStaffRow"];

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

const input = (r: Row, p: Partial<{ showOnWebsite: boolean; jobTitle: string | null; bio: string | null; websiteOrder: number }>) => ({
  showOnWebsite: r.showOnWebsite,
  jobTitle: r.jobTitle,
  bio: r.bio,
  websiteOrder: r.websiteOrder,
  ...p,
});

/** The quick switch: show or hide this person on the website's Staff page. */
export function StaffWebsiteSwitch({ row, nextOrder }: { row: Row; nextOrder: number }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [busy, setBusy] = useState(false);
  return (
    <Switch
      checked={row.showOnWebsite}
      disabled={busy}
      label={`Show ${row.name} on the website`}
      onCheckedChange={async (v) => {
        setBusy(true);
        const r = await runAction(() => saveStaffWebsite(row.staffId, input(row, { showOnWebsite: v, websiteOrder: v && !row.showOnWebsite ? nextOrder : row.websiteOrder })), {
          success: v ? `${row.name} is on the website` : `${row.name} is hidden from the website`,
        });
        setBusy(false);
        if (r) startTransition(() => router.refresh());
      }}
    />
  );
}

/** Edit what the website says about a staff member: title, a short bio and their position on the page. */
export function StaffWebsiteDialog({ row }: { row: Row }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");
  const [bio, setBio] = useState("");
  const [order, setOrder] = useState("0");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);
  const server = problem?.errors ?? {};
  const local = {
    title: title.length > 100 ? "100 characters at most." : undefined,
    bio: bio.length > 2000 ? "2000 characters at most." : undefined,
    order: /^\d{1,3}$/.test(order) ? undefined : "A whole number, 0 to 999.",
  };
  const errors = { title: local.title ?? server.jobTitle?.[0], bio: local.bio ?? server.bio?.[0], order: local.order ?? server.websiteOrder?.[0] };
  const id = `sw-${row.staffId}`;

  const onOpenChange = (v: boolean) => {
    if (v) {
      setTitle(row.jobTitle ?? "");
      setBio(row.bio ?? "");
      setOrder(String(row.websiteOrder));
      setProblem(null);
    }
    setOpen(v);
  };

  return (
    <FormDialog
      title={`${row.name} on the website`}
      open={open}
      onOpenChange={onOpenChange}
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Edit ${row.name} on the website`}>
          <Pencil className="size-4" aria-hidden />
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4 p-5">
          {problem && !problem.errors && (
            <Alert tone="danger" title="Couldn't save">
              {problemMessage(problem)}
            </Alert>
          )}
          <div className="grid gap-4 sm:grid-cols-[1fr_7rem]">
            <Field id={`${id}-title`} label="Title shown" error={errors.title} hint="e.g. Head of Primary">
              <Input value={title} onChange={(e) => setTitle(e.target.value)} {...describedBy(`${id}-title`, errors.title, true)} />
            </Field>
            <Field id={`${id}-order`} label="Position" error={errors.order} hint="Lower first">
              <Input value={order} onChange={(e) => setOrder(e.target.value)} inputMode="numeric" className="tabular" {...describedBy(`${id}-order`, errors.order, true)} />
            </Field>
          </div>
          <Field id={`${id}-bio`} label="Short bio (optional)" error={errors.bio} hint={`${bio.length}/2000. Qualifications, what they teach, a line about them.`}>
            <textarea value={bio} onChange={(e) => setBio(e.target.value)} rows={5} className={textareaClass} {...describedBy(`${id}-bio`, errors.bio, true)} />
          </Field>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Saving…"
              disabled={Boolean(local.title || local.bio || local.order)}
              onClick={async () => {
                setProblem(null);
                setBusy(true);
                const r = await runAction(() => saveStaffWebsite(row.staffId, input(row, { jobTitle: title.trim() || null, bio: bio.trim() || null, websiteOrder: Number(order) })), {
                  success: "Saved",
                  onProblem: setProblem,
                });
                setBusy(false);
                if (r) {
                  close();
                  startTransition(() => router.refresh());
                }
              }}
            >
              <Save className="size-4" aria-hidden />
              Save
            </Button>
          </div>
        </div>
      )}
    </FormDialog>
  );
}
