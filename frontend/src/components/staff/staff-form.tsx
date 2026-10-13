"use client";

import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Lock } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { StaffCreated, StaffDetail } from "@/lib/api/types";
import { createStaff, updateStaff } from "@/lib/staff/actions";
import { runAction } from "@/lib/client/action";
import { STAFF_ROLES, roleDescriptions, staffSchema, type StaffFormValues, type StaffPayload } from "@/lib/validation/staff";
import { Button, Field, Input, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Switch } from "@/components/ui/switch";

const fields = ["fullName", "email", "phone", "jobTitle", "roles"] as const;
const conflictField: Record<string, (typeof fields)[number]> = {
  "staff.duplicate_email": "email",
  "staff.email_in_use": "email",
  "staff.email_is_student": "email",
};

type Props =
  | { mode: "create"; onCreated: (created: StaffCreated) => void; footer?: React.ReactNode }
  | { mode: "edit"; staff: StaffDetail; self: boolean; onSaved?: (staff: StaffDetail) => void };

/** Add or edit a staff member. On your own record, Admin and Active are locked (the API refuses self-lockout). */
export function StaffForm(props: Props) {
  const staff = props.mode === "edit" ? props.staff : undefined;
  const self = props.mode === "edit" && props.self;
  const [problem, setProblem] = useState<Problem | null>(null);
  const [confirmDeactivate, setConfirmDeactivate] = useState(false);

  const {
    register,
    handleSubmit,
    setError,
    control,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<StaffFormValues, unknown, StaffPayload>({
    resolver: zodResolver(staffSchema),
    defaultValues: {
      fullName: staff?.fullName ?? "",
      email: staff?.email ?? "",
      phone: staff?.phone ?? "",
      jobTitle: staff?.jobTitle ?? "",
      roles: (staff?.roles.filter((r) => (STAFF_ROLES as readonly string[]).includes(r)) as StaffFormValues["roles"]) ?? ["Teacher"],
      isActive: staff?.isActive ?? true,
    },
  });

  const onProblem = (p: Problem) => {
    const field = p.code ? conflictField[p.code] : undefined;
    if (field) setError(field, { type: "server", message: problemMessage(p) }, { shouldFocus: true });
    else setProblem(p);
  };

  const onSubmit = handleSubmit(async ({ isActive, ...body }) => {
    setProblem(null);
    if (props.mode === "create") {
      const created = await runAction(() => createStaff(body), {
        success: (c) => `${c.staff.fullName} added`,
        fields,
        setError,
        onProblem,
      });
      if (created) props.onCreated(created);
      return;
    }
    const saved = await runAction(() => updateStaff(props.staff.id, { ...body, isActive }), {
      success: (s) => `${s.fullName} saved`,
      fields,
      setError,
      onProblem,
    });
    if (saved) {
      reset({ ...body, phone: saved.phone ?? "", jobTitle: saved.jobTitle ?? "", isActive: saved.isActive });
      props.onSaved?.(saved);
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {problem && problem.status > 0 && problem.status !== 429 && problem.status < 500 && (
        <Alert tone="danger" className="animate-rise" title="Couldn't save">
          <p>{problemMessage(problem)}</p>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="staff-name" label="Full name" error={errors.fullName?.message} className="sm:col-span-2">
          <Input {...describedBy("staff-name", errors.fullName?.message)} {...register("fullName")} autoComplete="off" placeholder="e.g. Mrs. Grace Okafor" />
        </Field>
        <Field id="staff-email" label="Email" error={errors.email?.message} hint={props.mode === "create" ? "This becomes their username." : "Also their username."}>
          <Input {...describedBy("staff-email", errors.email?.message, true)} {...register("email")} type="email" autoComplete="off" spellCheck={false} />
        </Field>
        <Field id="staff-phone" label="Phone (optional)" error={errors.phone?.message}>
          <Input {...describedBy("staff-phone", errors.phone?.message)} {...register("phone")} type="tel" inputMode="tel" autoComplete="off" placeholder="+234 803 123 4567" />
        </Field>
        <Field id="staff-job" label="Job title (optional)" error={errors.jobTitle?.message} className="sm:col-span-2">
          <Input {...describedBy("staff-job", errors.jobTitle?.message)} {...register("jobTitle")} autoComplete="off" placeholder="e.g. Class teacher, Head of Science" />
        </Field>
      </div>

      <Controller
        control={control}
        name="roles"
        render={({ field }) => (
          <fieldset aria-describedby={errors.roles ? "staff-roles-error" : undefined}>
            <legend className="mb-1.5 text-sm font-medium">Roles</legend>
            <div className="grid gap-2 sm:grid-cols-3">
              {STAFF_ROLES.map((role) => {
                const checked = field.value.includes(role);
                const locked = self && role === "Admin" && checked;
                return (
                  <label
                    key={role}
                    htmlFor={`staff-role-${role}`}
                    aria-label={`${role}: ${roleDescriptions[role]}`}
                    className={cn(
                      "flex cursor-pointer gap-3 rounded-input border p-3 transition-colors",
                      checked ? "border-brand bg-brand/5" : "border-border hover:bg-surface-2",
                      locked && "cursor-not-allowed",
                    )}
                  >
                    <input
                      id={`staff-role-${role}`}
                      type="checkbox"
                      className="mt-0.5 size-4 shrink-0 accent-[var(--brand)]"
                      checked={checked}
                      disabled={locked}
                      onChange={(e) => field.onChange(e.target.checked ? [...field.value, role] : field.value.filter((r) => r !== role))}
                    />
                    <span>
                      <span className="flex items-center gap-1.5 text-sm font-medium">
                        {role}
                        {locked && <Lock className="size-3.5 text-muted" aria-label="You can't remove your own Admin role" />}
                      </span>
                      <span className="block text-xs text-muted">{roleDescriptions[role]}</span>
                    </span>
                  </label>
                );
              })}
            </div>
            {errors.roles && (
              <p id="staff-roles-error" role="alert" className="mt-1.5 text-sm text-danger">
                {errors.roles.message}
              </p>
            )}
          </fieldset>
        )}
      />

      {props.mode === "edit" && (
        <Controller
          control={control}
          name="isActive"
          render={({ field }) => (
            <div className="flex items-center justify-between gap-4 rounded-input border border-border p-3">
              <div>
                <p className="text-sm font-medium">Active</p>
                <p className="text-xs text-muted">
                  {self ? "You can't deactivate your own account." : "Inactive staff can't sign in and drop out of teacher pickers."}
                </p>
              </div>
              <Switch
                checked={field.value}
                disabled={self}
                label="Active"
                onCheckedChange={(v) => (v ? field.onChange(true) : setConfirmDeactivate(true))}
              />
              <ConfirmDialog
                open={confirmDeactivate}
                onOpenChange={setConfirmDeactivate}
                title={`Deactivate ${props.staff.fullName}?`}
                confirmLabel="Deactivate"
                tone="destructive"
                onConfirm={async () => {
                  field.onChange(false);
                  return true;
                }}
              >
                <p>They&apos;ll be signed out everywhere when you save, and won&apos;t be able to sign in. Their class assignments stay until you reassign them.</p>
              </ConfirmDialog>
            </div>
          )}
        />
      )}

      <div className="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
        {props.mode === "create" ? props.footer : null}
        <Button type="submit" loading={isSubmitting} disabled={props.mode === "edit" && !isDirty}>
          {props.mode === "create" ? "Add staff member" : "Save changes"}
        </Button>
      </div>
    </form>
  );
}
