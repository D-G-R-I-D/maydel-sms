"use client";

import { useEffect, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { FeeItem } from "@/lib/api/types";
import { createFeeItem, updateFeeItem } from "@/lib/finance/actions";
import { runAction } from "@/lib/client/action";
import { useFormDraft } from "@/lib/drafts/use-form-draft";
import { feeItemSchema, type FeeItemFormValues, type FeeItemPayload } from "@/lib/validation/finance";
import { audienceLabel, defaultChargedByDefault } from "@/lib/finance/rules";
import { Button, Field, Input, Select, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, DialogActions } from "@/components/ui/dialog";
import { Switch } from "@/components/ui/switch";
import { DraftBanner } from "@/components/drafts/draft-banner";

const fields = ["name", "amount", "classLevelId"] as const;

/** Add or edit a fee item for a term (E10). Making an item mandatory clears its opt-ins (API). */
export function FeeItemForm({
  termId,
  levels,
  item,
  close,
}: {
  termId: string;
  levels: { id: string; name: string }[];
  item?: FeeItem;
  close: () => void;
}) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const form = useForm<FeeItemFormValues, unknown, FeeItemPayload>({
    resolver: zodResolver(feeItemSchema),
    defaultValues: {
      name: item?.name ?? "",
      amount: item ? item.amount.toFixed(2) : "",
      classLevelId: item?.classLevelId ?? "",
      isMandatory: item?.isMandatory ?? true,
      siblingDiscount: item?.siblingDiscount ?? false,
      audience: item?.audience ?? "All",
      chargedByDefault: item?.chargedByDefault ?? false,
    },
  });
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    control,
    formState: { errors, isSubmitting },
  } = form;
  const draft = useFormDraft(form, { scope: ["fee-item", item?.id ?? `new:${termId}`] });
  const mandatory = useWatch({ control, name: "isMandatory" });
  const audience = useWatch({ control, name: "audience" });
  // Like the API: optional new-intake items (uniform pack, books) are charged by default so nobody
  // forgets them; others aren't. Follows the "Applies to" choice until the user flips the switch.
  const [userSetMode, setUserSetMode] = useState(false);
  useEffect(() => {
    if (!item && !userSetMode) setValue("chargedByDefault", defaultChargedByDefault(audience));
  }, [audience, item, userSetMode, setValue]);

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    const body = { termId, ...values };
    const saved = await runAction(() => (item ? updateFeeItem(item.id, body) : createFeeItem(body)), {
      success: (s) => `${s.name} ${item ? "saved" : "added"}`,
      fields,
      setError,
      onProblem: setProblem,
    });
    if (saved) {
      draft.clear();
      close();
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      <DraftBanner draft={draft} onDiscard={draft.discard} />
      {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
        <Alert tone="danger" title="Couldn't save">
          {problemMessage(problem)}
        </Alert>
      )}
      <Field id="fee-name" label="Name" error={errors.name?.message}>
        <Input {...describedBy("fee-name", errors.name?.message)} {...register("name")} autoComplete="off" placeholder="e.g. Tuition, School bus, Lunch" />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="fee-amount" label="Amount (₦)" error={errors.amount?.message}>
          <Input {...describedBy("fee-amount", errors.amount?.message)} {...register("amount")} inputMode="decimal" autoComplete="off" placeholder="150,000" className="tabular" />
        </Field>
        <Field id="fee-level" label="Class level" error={errors.classLevelId?.message}>
          <Select {...describedBy("fee-level", errors.classLevelId?.message)} {...register("classLevelId")}>
            <option value="">All classes</option>
            {levels.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name} only
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <Field id="fee-audience" label="Applies to" hint="New intake: admitted this term or up to 120 days before it starts.">
        <Select {...describedBy("fee-audience", undefined, true)} {...register("audience")}>
          {(["All", "NewIntake", "Returning"] as const).map((a) => (
            <option key={a} value={a}>
              {audienceLabel[a]}
            </option>
          ))}
        </Select>
      </Field>
      <Controller
        control={control}
        name="isMandatory"
        render={({ field }) => (
          <div className="flex items-start justify-between gap-4 rounded-input border border-border p-3">
            <div>
              <p id="fee-mandatory-label" className="text-sm font-medium">
                {field.value ? "Mandatory" : "Optional"}
              </p>
              <p className="text-xs text-muted">{field.value ? "Billed to every student it applies to." : "Billed according to “Who pays this?” (e.g. bus, lunch, books)."}</p>
              {item && !item.isMandatory && field.value && item.optedIn > 0 && <p className="mt-1 text-xs text-warning">Making it mandatory clears the {item.optedIn} opt-ins.</p>}
            </div>
            <Switch checked={field.value} onCheckedChange={field.onChange} label="Mandatory" />
          </div>
        )}
      />
      {!mandatory && (
        <Controller
          control={control}
          name="chargedByDefault"
          render={({ field }) => (
            <div className="flex items-start justify-between gap-4 rounded-input border border-border p-3">
              <div>
                <p className="text-sm font-medium">Charged by default</p>
                <p className="text-xs text-muted">On: everyone pays unless unticked (books, lunch). Off: only ticked students pay (bus).</p>
                {item && !item.isMandatory && field.value !== item.chargedByDefault && (item.optedIn > 0 || item.optedOut > 0) && (
                  <p className="mt-1 text-xs text-warning">Switching clears the current ticks.</p>
                )}
              </div>
              <Switch
                checked={field.value}
                onCheckedChange={(v) => {
                  setUserSetMode(true);
                  field.onChange(v);
                }}
                label="Charged by default"
              />
            </div>
          )}
        />
      )}
      <Controller
        control={control}
        name="siblingDiscount"
        render={({ field }) => (
          <div className="flex items-start justify-between gap-4 rounded-input border border-border p-3">
            <div>
              <p className="text-sm font-medium">Sibling discount applies</p>
              <p className="text-xs text-muted">Usually just tuition. Set the discount in Settings → Fees.{!mandatory && " Applies to optional items too, for those who take them."}</p>
            </div>
            <Switch checked={field.value} onCheckedChange={field.onChange} label="Sibling discount applies" />
          </div>
        )}
      />
      <DialogActions>
        <CancelButton />
        <Button type="submit" loading={isSubmitting} loadingText="Saving…">
          {item ? "Save fee" : "Add fee"}
        </Button>
      </DialogActions>
    </form>
  );
}
