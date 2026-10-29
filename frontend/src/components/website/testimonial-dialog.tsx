"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition, type ReactNode } from "react";
import { Save, Trash2 } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { deleteTestimonial, saveTestimonial } from "@/lib/website/actions";
import { testimonialErrors, type Testimonial } from "@/lib/website/cms";
import { ConfirmDialog, FormDialog } from "@/components/ui/dialog";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

/** Add or edit a family's words for the website. `item` null = new. */
export function TestimonialDialog({ item, nextOrder, trigger }: { item: Testimonial | null; nextOrder: number; trigger: ReactNode }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [open, setOpen] = useState(false);
  const [quote, setQuote] = useState("");
  const [attribution, setAttribution] = useState("");
  const [order, setOrder] = useState("0");
  const [published, setPublished] = useState(true);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const local = testimonialErrors({ quote, attribution, order });
  const errors = { quote: local.quote ?? problem?.errors?.quote?.[0], attribution: local.attribution ?? problem?.errors?.attribution?.[0], order: local.order };
  const id = item ? `t-${item.id}` : "t-new";

  // Each time the dialog opens, the form starts from the saved values.
  const onOpenChange = (v: boolean) => {
    if (v) {
      setQuote(item?.quote ?? "");
      setAttribution(item?.attribution ?? "");
      setOrder(String(item?.order ?? nextOrder));
      setPublished(item?.isPublished ?? true);
      setProblem(null);
    }
    setOpen(v);
  };

  return (
    <FormDialog title={item ? "Edit testimonial" : "Add a testimonial"} description="Shown in “Voices from the Maydel family” on the homepage." trigger={trigger} open={open} onOpenChange={onOpenChange}>
      {(close) => (
        <div className="space-y-4 p-5">
          {problem && !problem.errors && (
            <Alert tone="danger" title="Couldn't save">
              {problemMessage(problem)}
            </Alert>
          )}
          <Field id={`${id}-quote`} label="What they said" error={errors.quote} hint={`${quote.length}/1000`}>
            <textarea value={quote} onChange={(e) => setQuote(e.target.value)} rows={5} className={textareaClass} {...describedBy(`${id}-quote`, errors.quote, true)} />
          </Field>
          <Field id={`${id}-who`} label="Who said it" error={errors.attribution} hint="No names unless the family agrees, e.g. “Parent of a Year 3 pupil”.">
            <Input value={attribution} onChange={(e) => setAttribution(e.target.value)} {...describedBy(`${id}-who`, errors.attribution, true)} />
          </Field>
          <div className="flex flex-wrap items-end gap-6">
            <Field id={`${id}-order`} label="Order" error={errors.order} hint="Lower shows first.">
              <Input value={order} onChange={(e) => setOrder(e.target.value)} inputMode="numeric" className="tabular w-24" {...describedBy(`${id}-order`, errors.order, true)} />
            </Field>
            <span className="flex items-center gap-3 pb-2 text-sm">
              <Switch checked={published} onCheckedChange={setPublished} label="Show on the website" />
              <span aria-hidden>{published ? "On the website" : "Hidden"}</span>
            </span>
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Saving…"
              disabled={Boolean(local.quote || local.attribution || local.order)}
              onClick={async () => {
                setProblem(null);
                setBusy(true);
                const r = await runAction(() => saveTestimonial(item?.id ?? null, { quote: quote.trim(), attribution: attribution.trim(), order: Number(order), isPublished: published }), {
                  success: item ? "Testimonial saved" : "Testimonial added",
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

export function DeleteTestimonial({ item }: { item: Testimonial }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  return (
    <ConfirmDialog
      title="Delete this testimonial?"
      confirmLabel="Delete"
      pendingLabel="Deleting…"
      tone="destructive"
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Delete testimonial from ${item.attribution}`}>
          <Trash2 className="size-4" aria-hidden />
        </Button>
      }
      onConfirm={async () => {
        const r = await runAction(() => deleteTestimonial(item.id), { success: "Testimonial deleted" });
        if (r) startTransition(() => router.refresh());
        return Boolean(r);
      }}
    >
      &ldquo;{item.quote.slice(0, 120)}
      {item.quote.length > 120 ? "…" : ""}&rdquo; will be removed from the website. To keep it but hide it, edit it and switch off &ldquo;Show on the website&rdquo; instead.
    </ConfirmDialog>
  );
}
