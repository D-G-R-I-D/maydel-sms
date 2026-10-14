"use client";

import { useFormDraft } from "@/lib/drafts/use-form-draft";
import { DraftBanner } from "@/components/drafts/draft-banner";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { Controller, useForm, type Control } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ExternalLink, Mail, Pencil, Phone, Star, Trash2, UserPlus, UsersRound } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { GuardianLink, GuardianListItem } from "@/lib/api/types";
import { addGuardian, findGuardians, removeGuardian, updateGuardianLink } from "@/lib/students/actions";
import { runAction } from "@/lib/client/action";
import {
  RELATIONSHIPS,
  addGuardianSchema,
  guardianLinkSchema,
  looksLikePhone,
  phoneKey,
  type AddGuardianPayload,
  type AddGuardianValues,
  type GuardianLinkValues,
} from "@/lib/validation/students";
import { Badge, Button, Card, Field, Input, Select, cn, describedBy } from "@/components/ui/primitives";
import { Alert, EmptyState } from "@/components/ui/states";
import { CancelButton, ConfirmDialog, DialogActions, FormDialog } from "@/components/ui/dialog";
import { Switch } from "@/components/ui/switch";
import { toast } from "@/components/ui/toast";
import { IssueLoginButton } from "@/components/credentials/issue-login";

const addFields = ["fullName", "phone", "email", "occupation", "address", "relationship"] as const;

/** Primary-contact and pick-up switches; both forms share these two field names. */
function LinkSwitches({ control: c }: { control: Control<GuardianLinkValues> }) {
  return (
    <div className="grid gap-2 sm:grid-cols-2">
      <Controller
        control={c}
        name="isPrimaryContact"
        render={({ field }) => (
          <div className="flex items-center justify-between gap-3 rounded-input border border-border p-3">
            <div>
              <p className="text-sm font-medium">Primary contact</p>
              <p className="text-xs text-muted">One per student; replaces the current one.</p>
            </div>
            <Switch checked={field.value} onCheckedChange={field.onChange} label="Primary contact" />
          </div>
        )}
      />
      <Controller
        control={c}
        name="canPickUp"
        render={({ field }) => (
          <div className="flex items-center justify-between gap-3 rounded-input border border-border p-3">
            <div>
              <p className="text-sm font-medium">Can pick up</p>
              <p className="text-xs text-muted">Allowed to collect the child.</p>
            </div>
            <Switch checked={field.value} onCheckedChange={field.onChange} label="Can pick up" />
          </div>
        )}
      />
    </div>
  );
}

/** Phone first: as they type, look for an existing guardian (siblings share one guardian). */
function AddGuardianForm({ studentId, isFirst, close }: { studentId: string; isFirst: boolean; close: () => void }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const [matches, setMatches] = useState<GuardianListItem[]>([]);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  useEffect(() => () => clearTimeout(timer.current), []);

  const form = useForm<AddGuardianValues, unknown, AddGuardianPayload>({
    resolver: zodResolver(addGuardianSchema),
    defaultValues: { fullName: "", phone: "", email: "", occupation: "", address: "", relationship: "Mother", isPrimaryContact: isFirst, canPickUp: true },
  });
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors, isSubmitting },
  } = form;
  const draft = useFormDraft(form, { scope: ["add-guardian", studentId] });

  const phoneField = register("phone");
  const lookup = (value: string) => {
    clearTimeout(timer.current);
    if (!looksLikePhone(value)) {
      setMatches([]);
      return;
    }
    timer.current = setTimeout(async () => {
      const r = await findGuardians(value).catch(() => null);
      const key = phoneKey(value);
      setMatches(r?.ok ? r.data.filter((g) => phoneKey(g.phone) === key) : []);
    }, 400);
  };
  const existing = matches[0];

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    const added = await runAction(() => addGuardian(studentId, values), {
      success: (a) => {
        const g = a.student.guardians.find((x) => x.guardianId === a.guardianId);
        return a.reusedExistingGuardian ? `Linked to existing guardian ${g?.fullName ?? ""}` : `${g?.fullName ?? "Guardian"} added`;
      },
      fields: addFields,
      setError,
      onProblem: setProblem,
    });
    if (!added) return;
    draft.clear();
    if (added.reusedExistingGuardian) {
      const g = added.student.guardians.find((x) => x.guardianId === added.guardianId);
      const others = existing ? existing.childCount : 0;
      toast.info(`Linked to existing guardian ${g?.fullName ?? ""}`, {
        description: `${others > 0 ? `They're also guardian of ${others} other ${others === 1 ? "child" : "children"}. ` : ""}Their saved name and email were kept.`,
        duration: 8000,
      });
    }
    close();
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      <DraftBanner draft={draft} onDiscard={draft.discard} />
      {problem && problem.status > 0 && problem.status !== 429 && problem.status < 500 && (
        <Alert tone="danger" className="animate-rise" title={problem.code === "guardian.already_linked" ? "Already linked" : "Couldn't add guardian"}>
          <p>{problemMessage(problem)}</p>
        </Alert>
      )}
      <Field id="g-phone" label="Phone" error={errors.phone?.message} hint="Any format: 0803 000 0000 or +234 803 000 0000.">
        <Input
          {...describedBy("g-phone", errors.phone?.message, true)}
          {...phoneField}
          onChange={(e) => {
            void phoneField.onChange(e);
            lookup(e.target.value);
          }}
          type="tel"
          inputMode="tel"
          autoComplete="off"
        />
      </Field>
      {existing && (
        <Alert tone="info" title="Existing guardian found" className="animate-fade">
          <p>
            <span className="font-medium">{existing.fullName}</span> ({existing.childCount} {existing.childCount === 1 ? "child" : "children"}) has this phone. Saving links this student
            to them; their stored name and email are kept.
          </p>
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="g-name" label="Full name" error={errors.fullName?.message} className="sm:col-span-2">
          <Input {...describedBy("g-name", errors.fullName?.message)} {...register("fullName")} autoComplete="off" placeholder={existing?.fullName} />
        </Field>
        <Field id="g-relationship" label="Relationship" error={errors.relationship?.message}>
          <Select {...describedBy("g-relationship", errors.relationship?.message)} {...register("relationship")}>
            {RELATIONSHIPS.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </Select>
        </Field>
        <Field id="g-email" label="Email (optional)" error={errors.email?.message} hint="Becomes their username; otherwise their phone is.">
          <Input {...describedBy("g-email", errors.email?.message, true)} {...register("email")} type="email" autoComplete="off" spellCheck={false} />
        </Field>
        <Field id="g-occupation" label="Occupation (optional)" error={errors.occupation?.message}>
          <Input {...describedBy("g-occupation", errors.occupation?.message)} {...register("occupation")} autoComplete="off" />
        </Field>
        <Field id="g-address" label="Address (optional)" error={errors.address?.message}>
          <Input {...describedBy("g-address", errors.address?.message)} {...register("address")} autoComplete="off" />
        </Field>
      </div>
      <LinkSwitches control={control as unknown as Control<GuardianLinkValues>} />
      <DialogActions>
        <CancelButton />
        <Button type="submit" loading={isSubmitting} loadingText="Saving…">
          {existing ? "Link guardian" : "Add guardian"}
        </Button>
      </DialogActions>
    </form>
  );
}

function EditLinkForm({ studentId, guardian, close }: { studentId: string; guardian: GuardianLink; close: () => void }) {
  const form = useForm<GuardianLinkValues>({
    resolver: zodResolver(guardianLinkSchema),
    defaultValues: { relationship: guardian.relationship, isPrimaryContact: guardian.isPrimaryContact, canPickUp: guardian.canPickUp },
  });
  const {
    register,
    handleSubmit,
    control,
    formState: { isSubmitting },
  } = form;
  const draft = useFormDraft(form, { scope: ["guardian-link", studentId, guardian.guardianId] });
  const onSubmit = handleSubmit(async (values) => {
    const saved = await runAction(() => updateGuardianLink(studentId, guardian.guardianId, values), {
      success: `${guardian.fullName} updated`,
      onProblem: (p) => p.status > 0 && p.status < 500 && p.status !== 429 && toast.error("Couldn't save", { description: problemMessage(p) }),
    });
    if (saved) {
      draft.clear();
      close();
    }
  });
  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      <DraftBanner draft={draft} onDiscard={draft.discard} />
      <Field id="gl-relationship" label="Relationship">
        <Select id="gl-relationship" {...register("relationship")}>
          {RELATIONSHIPS.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </Select>
      </Field>
      <LinkSwitches control={control} />
      <p className="text-xs text-muted">
        Name, phone and email are shared by all their children. Edit them on the{" "}
        <Link href={`/admin/guardians/${guardian.guardianId}`} className="text-brand hover:underline">
          guardian&apos;s page
        </Link>
        .
      </p>
      <DialogActions>
        <CancelButton />
        <Button type="submit" loading={isSubmitting} loadingText="Saving…">
          Save
        </Button>
      </DialogActions>
    </form>
  );
}

/** Guardians on a student's profile: primary contact first; add, edit link, remove, issue parent login. */
export function GuardiansSection({ studentId, studentName, guardians, canEdit }: { studentId: string; studentName: string; guardians: GuardianLink[]; canEdit: boolean }) {
  const add = canEdit ? (
    <FormDialog
      title="Add guardian"
      description={`For ${studentName}. Siblings share one guardian record, matched by phone.`}
      trigger={
        <Button variant="secondary" size="sm">
          <UserPlus className="size-4" aria-hidden />
          Add guardian
        </Button>
      }
    >
      {(close) => <AddGuardianForm studentId={studentId} isFirst={guardians.length === 0} close={close} />}
    </FormDialog>
  ) : null;

  return (
    <Card className="p-5">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 className="font-display text-lg font-semibold">Guardians</h2>
        {add}
      </div>
      {guardians.length === 0 ? (
        <EmptyState icon={UsersRound} title="No guardians yet">
          {canEdit ? "Add a parent or guardian so the school can reach them and they can use the portal." : "No guardians have been added."}
        </EmptyState>
      ) : (
        <ul className="space-y-3">
          {guardians.map((g) => (
            <li key={g.guardianId} className={cn("rounded-input border p-4", g.isPrimaryContact ? "border-brand/40 bg-brand/5" : "border-border")}>
              <div className="flex flex-wrap items-start gap-x-3 gap-y-1">
                <div className="min-w-0 flex-1">
                  <p className="flex flex-wrap items-center gap-2 font-medium">
                    <Link href={canEdit ? `/admin/guardians/${g.guardianId}` : "#"} className={cn(canEdit && "hover:text-brand hover:underline")}>
                      {g.fullName}
                    </Link>
                    <Badge>{g.relationship}</Badge>
                    {g.isPrimaryContact && (
                      <Badge tone="brand">
                        <Star className="size-3" aria-hidden />
                        Primary contact
                      </Badge>
                    )}
                    {!g.canPickUp && <Badge tone="warning">Can&apos;t pick up</Badge>}
                    {!g.hasLogin && <Badge tone="warning">No login</Badge>}
                  </p>
                  <p className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted">
                    <a href={`tel:${g.phone}`} className="inline-flex min-h-8 items-center gap-1.5 hover:text-brand">
                      <Phone className="size-3.5" aria-hidden />
                      {g.phone}
                    </a>
                    {g.email && (
                      <a href={`mailto:${g.email}`} className="inline-flex min-h-8 items-center gap-1.5 break-all hover:text-brand">
                        <Mail className="size-3.5" aria-hidden />
                        {g.email}
                      </a>
                    )}
                  </p>
                </div>
                {canEdit && (
                  <div className="flex flex-wrap items-center gap-1">
                    <IssueLoginButton kind="guardian" id={g.guardianId} fullName={g.fullName} description={`Parent of ${studentName}`} hasLogin={g.hasLogin} size="sm" variant="ghost" />
                    <FormDialog
                      title={`${g.fullName} · ${studentName}`}
                      trigger={
                        <Button variant="ghost" size="sm" aria-label={`Edit ${g.fullName}'s link`}>
                          <Pencil className="size-4" aria-hidden />
                        </Button>
                      }
                    >
                      {(close) => <EditLinkForm studentId={studentId} guardian={g} close={close} />}
                    </FormDialog>
                    <Link href={`/admin/guardians/${g.guardianId}`} className="inline-flex size-9 items-center justify-center rounded-input text-muted hover:bg-surface-2 hover:text-text" aria-label={`Open ${g.fullName}`}>
                      <ExternalLink className="size-4" aria-hidden />
                    </Link>
                    <ConfirmDialog
                      title={`Remove ${g.fullName}?`}
                      confirmLabel="Remove"
                      pendingLabel="Removing…"
                      tone="destructive"
                      trigger={
                        <Button variant="ghost" size="sm" aria-label={`Remove ${g.fullName}`}>
                          <Trash2 className="size-4 text-danger" aria-hidden />
                        </Button>
                      }
                      onConfirm={async () =>
                        (await runAction(() => removeGuardian(studentId, g.guardianId), {
                          success: `${g.fullName} removed from ${studentName}`,
                          onProblem: (p) => p.status > 0 && p.status < 500 && toast.error("Couldn't remove", { description: problemMessage(p) }),
                        })) !== null
                      }
                    >
                      <p>
                        They&apos;re unlinked from {studentName} only; their record and other children stay.
                        {g.isPrimaryContact && " Another guardian becomes the primary contact."}
                      </p>
                    </ConfirmDialog>
                  </div>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
