"use client";

import { useRouter } from "next/navigation";
import { useCallback, useState, useTransition } from "react";
import { CalendarClock, FlaskConical, LayoutTemplate, Save, Send, Trash2, Undo2, Users } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { runAction } from "@/lib/client/action";
import { useDraft } from "@/lib/drafts/use-draft";
import { formatLagos } from "@/lib/time/lagos";
import { cancelNewsletter, countRecipients, deleteNewsletter, saveNewsletter, sendNewsletter, sendTest } from "@/lib/website/newsletter-actions";
import {
  formToNewsletterInput, newsletterAudiences, newsletterErrors, newsletterTemplates, newsletterToForm, scheduleAt, sections,
  type Newsletter, type NewsletterForm, type RecipientCount, type SchoolSection,
} from "@/lib/website/newsletters";
import { DraftBanner } from "@/components/drafts/draft-banner";
import { ConfirmDialog, FormDialog } from "@/components/ui/dialog";
import { Badge, Button, Card, Field, Input, Select, buttonClasses, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";
import { RichContent } from "./rich-content";
import { RichEditor } from "./rich-editor/rich-editor";

type ClassOption = { id: string; name: string };

/**
 * E12.6 newsletter editor: subject, preheader, formatted body (with templates), who gets it, and
 * Save draft · Send test · Schedule · Send now. Sent or scheduled newsletters are read-only.
 */
export function NewsletterEditor({ initial, initialCount, classes }: { initial: Newsletter | null; initialCount: RecipientCount | null; classes: ClassOption[] }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const [letter, setLetter] = useState(initial);
  const [saved, setSaved] = useState<NewsletterForm>(() => newsletterToForm(initial));
  const [form, setForm] = useState<NewsletterForm>(saved);
  const [editorKey, setEditorKey] = useState(0);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [busy, setBusy] = useState<"save" | "send" | null>(null);
  const [count, setCount] = useState<RecipientCount | null>(initialCount);

  const editable = !letter || letter.status === "Draft";
  const local = newsletterErrors(form);
  const server = problem?.errors ?? {};
  const errors = {
    title: local.title ?? server.title?.[0],
    preheader: local.preheader ?? server.preheader?.[0],
    bodyHtml: local.bodyHtml ?? server.bodyHtml?.[0],
    audience: local.audience ?? server.audience?.[0] ?? server.classRoomId?.[0],
  };
  const invalid = Object.keys(local).length > 0;
  const dirty = JSON.stringify(form) !== JSON.stringify(saved);
  const set = <K extends keyof NewsletterForm>(k: K, v: NewsletterForm[K]) => setForm((f) => ({ ...f, [k]: v }));

  const draft = useDraft({ scope: ["newsletter", letter?.id ?? "new"], value: form, dirty, server: saved, editable, onRestore: (v) => (setForm(v), setEditorKey((k) => k + 1)) });

  const refreshCount = useCallback(async (id: string) => {
    try {
      const r = await countRecipients(id);
      setCount(r.ok ? r.data : null);
    } catch {
      setCount(null);
    }
  }, []);

  /** Saves when needed and returns the saved newsletter (or null on failure). */
  const ensureSaved = async (): Promise<Newsletter | null> => {
    if (letter && !dirty) return letter;
    setProblem(null);
    setBusy("save");
    const r = await runAction(() => saveNewsletter(letter?.id ?? null, formToNewsletterInput(form)), { success: letter ? "Draft saved" : "Draft created", onProblem: setProblem });
    setBusy(null);
    if (!r) return null;
    const next = newsletterToForm(r);
    setLetter(r);
    setSaved(next);
    setForm(next);
    draft.clear();
    void refreshCount(r.id);
    if (!letter) startTransition(() => router.replace(`/admin/website/newsletters/${r.id}`));
    return r;
  };

  const send = async (at: string | null) => {
    const n = await ensureSaved();
    if (!n) return false;
    setBusy("send");
    const r = await runAction(() => sendNewsletter(n.id, at), {
      success: (x) => (x.status === "Scheduled" ? `Scheduled for ${formatLagos(x.scheduledFor!)}` : `Sending to ${x.recipients} people`),
      onProblem: setProblem,
    });
    setBusy(null);
    if (r) {
      setLetter(r);
      startTransition(() => router.refresh());
    }
    return Boolean(r);
  };

  const applyTemplate = (id: string) => {
    const t = newsletterTemplates(window.location.origin).find((x) => x.id === id);
    if (!t) return;
    setForm((f) => ({ ...f, title: f.title || t.title, preheader: f.preheader || t.preheader, bodyHtml: t.html }));
    setEditorKey((k) => k + 1);
  };

  if (letter && !editable) return <SentView letter={letter} onCancelled={(n) => (setLetter(n), setSaved(newsletterToForm(n)), setForm(newsletterToForm(n)))} />;

  return (
    <div className="space-y-5">
      {draft.restoredAt && <DraftBanner draft={draft} onDiscard={() => (draft.clear(), setForm(saved), setEditorKey((k) => k + 1))} />}
      {problem && !problem.errors && (
        <Alert tone="danger" title="Couldn't do that">
          {problemMessage(problem)}
        </Alert>
      )}
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
        <Card className="space-y-4 p-5">
          <Field id="nl-title" label="Subject" error={errors.title}>
            <Input value={form.title} onChange={(e) => set("title", e.target.value)} className="font-display text-lg" {...describedBy("nl-title", errors.title)} />
          </Field>
          <Field id="nl-pre" label="Preview line (optional)" error={errors.preheader} hint="The grey line inboxes show after the subject.">
            <Input value={form.preheader} onChange={(e) => set("preheader", e.target.value)} {...describedBy("nl-pre", errors.preheader, true)} />
          </Field>
          <div className="flex flex-wrap items-center gap-2 rounded-input bg-surface-2 p-2 text-sm">
            <LayoutTemplate className="size-4 text-brand" aria-hidden />
            <span className="text-muted">Start from a template:</span>
            {newsletterTemplates("").map((t) => (
              <Button key={t.id} size="sm" variant="secondary" onClick={() => applyTemplate(t.id)}>
                {t.label}
              </Button>
            ))}
          </div>
          <div className="space-y-1.5">
            <span className="block text-sm font-medium">Newsletter</span>
            <RichEditor key={editorKey} id="nl-body" label="Newsletter" initialHtml={form.bodyHtml} onChange={(html) => set("bodyHtml", html)} invalid={Boolean(errors.bodyHtml)} />
            {errors.bodyHtml && (
              <p className="text-sm text-danger" role="alert">
                {errors.bodyHtml}
              </p>
            )}
            <p className="text-xs text-muted">Using a template replaces what&apos;s in the editor.</p>
          </div>
        </Card>

        <div className="space-y-5">
          <Card className="space-y-3 p-5">
            <div className="flex items-center gap-2">
              <Badge>Draft</Badge>
              {dirty && <span className="text-xs text-warning">Unsaved changes</span>}
            </div>
            <Button variant="secondary" className="w-full" loading={busy === "save"} loadingText="Saving…" disabled={invalid || (!dirty && Boolean(letter))} onClick={() => void ensureSaved()}>
              <Save className="size-4" aria-hidden />
              Save draft
            </Button>
            <TestDialog disabled={invalid} ensureSaved={ensureSaved} />
            <ScheduleDialog disabled={invalid} onSchedule={(at) => send(at)} />
            <ConfirmDialog
              title="Send this newsletter now?"
              confirmLabel="Send now"
              pendingLabel="Sending…"
              trigger={
                <Button className="w-full" disabled={invalid || busy !== null}>
                  <Send className="size-4" aria-hidden />
                  Send now
                </Button>
              }
              onConfirm={() => send(null)}
            >
              {count ? (
                <>
                  It goes to <strong className="text-text">{count.total}</strong> {count.total === 1 ? "person" : "people"} ({count.parents} parents, {count.staff} staff, {count.subscribers} subscribers). This can&apos;t be undone.
                </>
              ) : (
                "It will be saved and sent to everyone in the audience. This can't be undone."
              )}
            </ConfirmDialog>
            {letter && (
              <ConfirmDialog
                title="Delete this draft?"
                confirmLabel="Delete"
                pendingLabel="Deleting…"
                tone="destructive"
                trigger={
                  <button type="button" className={cn(buttonClasses("ghost", "sm"), "text-danger")}>
                    <Trash2 className="size-4" aria-hidden />
                    Delete draft
                  </button>
                }
                onConfirm={async () => {
                  const r = await runAction(() => deleteNewsletter(letter.id), { success: "Draft deleted" });
                  if (r) {
                    draft.clear();
                    startTransition(() => router.replace("/admin/website/newsletters"));
                  }
                  return Boolean(r);
                }}
              >
                &ldquo;{letter.title}&rdquo; will be deleted.
              </ConfirmDialog>
            )}
          </Card>

          <Card className="space-y-3 p-5">
            <fieldset>
              <legend className="mb-2 text-sm font-medium">Who gets it</legend>
              <div className="space-y-1.5">
                {newsletterAudiences.map((a) => (
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
            {form.audience.includes("Parents") && (
              <div className="space-y-2 border-t border-border pt-3">
                <label htmlFor="nl-scope" className="block text-sm font-medium">
                  Which parents
                </label>
                <Select
                  id="nl-scope"
                  className="w-full"
                  value={form.scope.kind === "all" ? "all" : form.scope.kind === "section" ? `s:${form.scope.section}` : `c:${form.scope.classRoomId}`}
                  onChange={(e) => {
                    const v = e.target.value;
                    set("scope", v === "all" ? { kind: "all" } : v.startsWith("s:") ? { kind: "section", section: v.slice(2) as SchoolSection } : { kind: "class", classRoomId: v.slice(2) });
                  }}
                >
                  <option value="all">All parents</option>
                  <optgroup label="One section">
                    {sections.map((s) => (
                      <option key={s.value} value={`s:${s.value}`}>
                        {s.label}
                      </option>
                    ))}
                  </optgroup>
                  {classes.length > 0 && (
                    <optgroup label="One class">
                      {classes.map((c) => (
                        <option key={c.id} value={`c:${c.id}`}>
                          {c.name}
                        </option>
                      ))}
                    </optgroup>
                  )}
                </Select>
              </div>
            )}
            <p className="flex items-center gap-2 border-t border-border pt-3 text-sm">
              <Users className="size-4 text-brand" aria-hidden />
              {count && !dirty ? (
                <span>
                  <strong className="tabular">{count.total}</strong> {count.total === 1 ? "person" : "people"}
                  <span className="text-muted">
                    {" "}
                    · {count.parents} parents · {count.staff} staff · {count.subscribers} subscribers
                  </span>
                </span>
              ) : (
                <span className="text-muted">{letter ? "Save to update the count." : "Save the draft to see how many people it reaches."}</span>
              )}
            </p>
          </Card>

          <Card className="p-5">
            <span className="flex items-center gap-3 text-sm">
              <Switch checked={form.publishOnWebsite} onCheckedChange={(v) => set("publishOnWebsite", v)} label="Publish on the website once sent" />
              <span aria-hidden>Publish on the website once sent</span>
            </span>
            <p className="mt-1 text-xs text-muted">Sent newsletters with this on get a page under Newsletters on the website.</p>
          </Card>
        </div>
      </div>
    </div>
  );
}

function TestDialog({ disabled, ensureSaved }: { disabled: boolean; ensureSaved: () => Promise<Newsletter | null> }) {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const bad = email.trim() !== "" && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
  return (
    <FormDialog
      title="Send a test copy"
      description="One copy marked [Test], so you can check it in a real inbox."
      trigger={
        <Button variant="secondary" className="w-full" disabled={disabled}>
          <FlaskConical className="size-4" aria-hidden />
          Send test
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4 p-5">
          <Field id="nl-test" label="Send to (optional)" error={bad ? "Enter a valid email address." : undefined} hint="Leave empty to send it to your own address.">
            <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} {...describedBy("nl-test", bad ? "x" : undefined, true)} />
          </Field>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Sending…"
              disabled={bad}
              onClick={async () => {
                setBusy(true);
                const n = await ensureSaved();
                const r = n ? await runAction(() => sendTest(n.id, email.trim() || null), { success: `Test sent to ${email.trim() || "your address"}` }) : null;
                setBusy(false);
                if (r) close();
              }}
            >
              <Send className="size-4" aria-hidden />
              Send test
            </Button>
          </div>
        </div>
      )}
    </FormDialog>
  );
}

function ScheduleDialog({ disabled, onSchedule }: { disabled: boolean; onSchedule: (at: string) => Promise<boolean> }) {
  const [when, setWhen] = useState("");
  const [busy, setBusy] = useState(false);
  const at = scheduleAt(when);
  return (
    <FormDialog
      title="Schedule the newsletter"
      description="Abuja time (WAT), up to 90 days ahead. You can cancel it while it waits."
      trigger={
        <Button variant="secondary" className="w-full" disabled={disabled}>
          <CalendarClock className="size-4" aria-hidden />
          Schedule…
        </Button>
      }
    >
      {(close) => (
        <div className="space-y-4 p-5">
          <Field id="nl-when" label="Send at">
            <Input id="nl-when" type="datetime-local" value={when} onChange={(e) => setWhen(e.target.value)} />
          </Field>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
            <Button
              loading={busy}
              loadingText="Scheduling…"
              disabled={!at}
              onClick={async () => {
                setBusy(true);
                const ok = await onSchedule(at!);
                setBusy(false);
                if (ok) close();
              }}
            >
              <CalendarClock className="size-4" aria-hidden />
              Schedule
            </Button>
          </div>
        </div>
      )}
    </FormDialog>
  );
}

/** Scheduled or sent: the content read-only, delivery progress, and Cancel while scheduled. */
function SentView({ letter, onCancelled }: { letter: Newsletter; onCancelled: (n: Newsletter) => void }) {
  const router = useRouter();
  const [, startTransition] = useTransition();
  const d = letter.delivery;
  return (
    <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
      <Card className="space-y-3 p-5">
        <p className="font-display text-xl font-semibold">{letter.title}</p>
        {letter.preheader && <p className="text-sm text-muted">{letter.preheader}</p>}
        <RichContent html={letter.bodyHtml} className="border-t border-border pt-3" />
      </Card>
      <Card className="space-y-3 p-5">
        <Badge tone={letter.status === "Sent" ? "success" : "warning"}>{letter.status}</Badge>
        {letter.status === "Scheduled" && letter.scheduledFor && <p className="text-sm">Goes out {formatLagos(letter.scheduledFor)} (WAT).</p>}
        {letter.status === "Sent" && letter.sentAt && <p className="text-sm">Sent {formatLagos(letter.sentAt)}.</p>}
        <dl className="grid grid-cols-3 gap-2 text-center text-sm">
          {[
            { label: "Delivered", value: d.sent, tone: "text-success" },
            { label: "Waiting", value: d.pending, tone: "text-muted" },
            { label: "Failed", value: d.failed, tone: d.failed ? "text-danger" : "text-muted" },
          ].map((x) => (
            <div key={x.label} className="rounded-input bg-surface-2 p-2">
              <dt className="text-xs text-muted">{x.label}</dt>
              <dd className={cn("tabular font-display text-xl font-semibold", x.tone)}>{x.value}</dd>
            </div>
          ))}
        </dl>
        {d.pending > 0 && <p className="text-xs text-muted">Copies go out within the daily email limit; school notices always go first.</p>}
        {letter.webUrl && (
          <a href={letter.webUrl} target="_blank" rel="noopener noreferrer" className="text-sm font-medium text-brand hover:underline">
            View on the website
          </a>
        )}
        {letter.status === "Scheduled" && (
          <ConfirmDialog
            title="Cancel the schedule?"
            confirmLabel="Back to draft"
            pendingLabel="Cancelling…"
            trigger={
              <Button variant="secondary" className="w-full">
                <Undo2 className="size-4" aria-hidden />
                Cancel schedule
              </Button>
            }
            onConfirm={async () => {
              const r = await runAction(() => cancelNewsletter(letter.id), { success: "Back to draft" });
              if (r) {
                onCancelled(r);
                startTransition(() => router.refresh());
              }
              return Boolean(r);
            }}
          >
            It won&apos;t be sent, and you can edit it again.
          </ConfirmDialog>
        )}
      </Card>
    </div>
  );
}
