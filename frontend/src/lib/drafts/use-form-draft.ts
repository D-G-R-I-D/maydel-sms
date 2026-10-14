"use client";

import { useState } from "react";
import type { FieldValues, UseFormReturn } from "react-hook-form";
import { useDraft, type DraftState } from "./use-draft";

/**
 * useDraft for a React Hook Form form. Restoring keeps the server values as defaults, so the form
 * reads as dirty and Save is enabled. Call `draft.clear()` after a successful save. `server` defaults
 * to the form's initial default values.
 */
export function useFormDraft<V extends FieldValues, C, T>(
  form: UseFormReturn<V, C, T>,
  opts: { scope: (string | number)[]; server?: unknown; editable?: boolean },
): DraftState & { discard: () => void } {
  const [initialDefaults] = useState(() => form.formState.defaultValues);
  const value = form.watch() as V;
  const draft = useDraft<V>({
    scope: opts.scope,
    value,
    dirty: form.formState.isDirty,
    server: opts.server ?? initialDefaults,
    editable: opts.editable,
    onRestore: (v) => form.reset(v, { keepDefaultValues: true }),
  });
  return {
    ...draft,
    discard: () => {
      draft.clear();
      form.reset();
    },
  };
}
