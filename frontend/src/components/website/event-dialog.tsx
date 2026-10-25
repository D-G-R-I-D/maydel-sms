"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition, type ReactNode } from "react";
import { Save, Trash2 } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { deleteEvent, saveEvent } from "@/lib/website/actions";
import { audiences, eventErrors, eventToForm, eventToInput, type EventForm, type SiteEvent } from "@/lib/website/posts";
import { ConfirmDialog, FormDialog } from "@/components/ui/dialog";
import { Button, Field, Input, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { RichEditor } from "./rich-editor/rich-editor";

/** Add or edit a school event (E7): when, where, who sees it, and a formatted description. */
export function EventDialog({ item, trigger }: { item: SiteEvent | null; trigger: ReactNode }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<EventForm>(() => eventToForm(item));
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState(false);
  const local = eventErrors(form);
  const server = problem?.errors ?? {};
  const errors = {
    title: local.title ?? server.title?.[0],
    startsAt: local.startsAt ?? server.startsAt?.[0],
    endsAt: local.endsAt ?? server.endsAt?.[0],
    location: local.location ?? server.location?.[0],
    audience: local.audience ?? server.audience?.[0],
    description: server.descriptionHtml?.[0] ?? server.description?.[0],
  };
  const set = <K extends keyof EventForm>(k: K, v: EventForm[K]) => setForm((f) => ({ ...f, [k]: v }));
  const id = item ? `ev-${item.id}` : "ev-new";

  const onOpenChange = (v: boolean) => {
    if (v) {
      setForm(eventToForm(item));
      setProblem(null);
    }
    setOpen(v);
  };

  return (
    <FormDialog size="lg" title={item ? "Edit event" : "Add an event"} description="Times are Abuja time (WAT)." trigger={trigger} open={open} onOpenChange={onOpenChange}>
      {(close) => (
        <div className="space-y-4 p-5">
          {problem && !problem.errors && (
            <Alert tone="danger" title="Couldn't save">
              {problemMessage(problem)}
            </Alert>
          )}
          <Field id={`${id}-title`} label="Title" error={errors.title}>
            <Input value={form.title} onChange={(e) => set("title", e.target.value)} {...describedBy(`${id}-title`, errors.title)} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id={`${id}-start`} label="Starts" error={errors.startsAt}>
              <Input type="datetime-local" value={form.startsAt} onChange={(e) => set("startsAt", e.target.value)} {...describedBy(`${id}-start`, errors.startsAt)} />
            </Field>
            <Field id={`${id}-end`} label="Ends (optional)" error={errors.endsAt}>
              <Input type="datetime-local" value={form.endsAt} onChange={(e) => set("endsAt", e.target.value)} {...describedBy(`${id}-end`, errors.endsAt)} />
            </Field>
          </div>
          <Field id={`${id}-where`} label="Where (optional)" error={errors.location} hint="e.g. School hall, or the sports pitch">
            <Input value={form.location} onChange={(e) => set("location", e.target.value)} {...describedBy(`${id}-where`, errors.location, true)} />
          </Field>
          <fieldset>
            <legend className="mb-2 text-sm font-medium">Who sees it</legend>
            <div className="flex flex-wrap gap-x-5 gap-y-2">
              {audiences.map((a) => (
                <label key={a.value} className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    className="size-4 accent-brand"
                    checked={form.audience.includes(a.value)}
                    onChange={(e) => set("audience", e.target.checked ? [...form.audience, a.value] : form.audience.filter((v) => v !== a.value))}
                  />
                  {a.label}
                </label>
              ))}
            </div>
            {errors.audience && (
              <p className="mt-1 text-sm text-danger" role="alert">
                {errors.audience}
              </p>
            )}
          </fieldset>
          <div className="space-y-1.5">
            <span className="block text-sm font-medium">Description (optional)</span>
            <RichEditor id={`${id}-desc`} label="Event description" initialHtml={form.descriptionHtml} onChange={(html) => set("descriptionHtml", html)} invalid={Boolean(errors.description)} />
            {errors.description && (
              <p className="text-sm text-danger" role="alert">
                {errors.description}
              </p>
            )}
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Saving…"
              disabled={Object.values(local).some(Boolean)}
              onClick={async () => {
                setProblem(null);
                setBusy(true);
                const r = await runAction(() => saveEvent(item?.id ?? null, eventToInput(form)), { success: item ? "Event saved" : "Event added", onProblem: setProblem });
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

export function DeleteEvent({ item }: { item: SiteEvent }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  return (
    <ConfirmDialog
      title="Delete this event?"
      confirmLabel="Delete"
      pendingLabel="Deleting…"
      tone="destructive"
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Delete ${item.title}`}>
          <Trash2 className="size-4" aria-hidden />
        </Button>
      }
      onConfirm={async () => {
        const r = await runAction(() => deleteEvent(item.id), { success: "Event deleted" });
        if (r) startTransition(() => router.refresh());
        return Boolean(r);
      }}
    >
      &ldquo;{item.title}&rdquo; will be removed from the website and the calendar.
    </ConfirmDialog>
  );
}
