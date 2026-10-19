"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Controller, useForm, useWatch, type FieldPath } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import { createTest, updateTest } from "@/lib/cbt/actions";
import { headerValues, newHeaderValues, parseScope, scopeKey, testHeaderSchema, toTestInput, type ComponentOption, type TestDetail, type TestHeaderValues } from "@/lib/cbt/tests";
import type { AuthoringScope } from "@/lib/cbt/questions";
import type { TermOption } from "@/components/assessment/term-picker";
import { runAction } from "@/lib/client/action";
import { Button, Field, Input, Select, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { Switch } from "@/components/ui/switch";

const textareaClass =
  "block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm";

export type ArmOption = { id: string; name: string; classLevelId: string };
type Values = TestHeaderValues;

/** API field → form field, for 400s. */
const serverField: Record<string, FieldPath<Values>> = {
  title: "title",
  instructions: "instructions",
  subjectId: "scope",
  classLevelId: "scope",
  termId: "termId",
  componentId: "componentId",
  classRoomIds: "classRoomIds",
  durationMinutes: "durationMinutes",
  opensAt: "opensTime",
  closesAt: "closesTime",
  maxAttempts: "maxAttempts",
};

/**
 * E16.2 header step: what the test is, who sits it and when. `test` = editing a draft's details;
 * subject and class are fixed once it has sections. Times are entered in Abuja time (WAT).
 */
export function TestHeaderForm({
  test,
  termId,
  scopes,
  terms,
  components,
  arms,
  onSaved,
}: {
  test: TestDetail | null;
  termId: string;
  scopes: AuthoringScope[];
  terms: TermOption[];
  components: ComponentOption[];
  arms: ArmOption[];
  onSaved?: (t: TestDetail) => void;
}) {
  const router = useRouter();
  const [problem, setProblem] = useState<Problem | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    getValues,
    control,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(testHeaderSchema), defaultValues: test ? headerValues(test) : newHeaderValues(termId) });
  const scope = useWatch({ control, name: "scope" });
  const chosen = useWatch({ control, name: "classRoomIds" });
  const levelArms = arms.filter((a) => a.classLevelId === parseScope(scope).classLevelId);
  const scopeLocked = Boolean(test && test.sections.length > 0);
  // The test's own scope stays selectable even if the caller no longer teaches it.
  const scopeOptions = test && !scopes.some((s) => scopeKey(s.subjectId, s.classLevelId) === scopeKey(test.subjectId, test.classLevelId))
    ? [...scopes, { subjectId: test.subjectId, subject: test.subject, classLevelId: test.classLevelId, classLevel: test.classLevel, questions: 0 }]
    : scopes;

  const save = handleSubmit(async (v) => {
    setProblem(null);
    const body = toTestInput(v);
    const r = await runAction(() => (test ? updateTest(test.id, body) : createTest(body)), {
      success: test ? "Details saved" : "Test created. Now add sections.",
      onProblem: (p) => {
        let applied = false;
        if (p.status === 400 && p.errors) {
          for (const [key, msgs] of Object.entries(p.errors)) {
            const f = serverField[key.charAt(0).toLowerCase() + key.slice(1)];
            if (f) {
              setError(f, { type: "server", message: msgs.join(" ") }, { shouldFocus: !applied });
              applied = true;
            }
          }
        }
        if (!applied) setProblem(p);
      },
    });
    if (!r) return;
    if (test) onSaved?.(r);
    else router.push(`/admin/tests/${r.id}`);
  });

  const err = (f: keyof Values) => (errors[f] as { message?: string } | undefined)?.message;
  const armsError = errors.classRoomIds?.message ?? errors.classRoomIds?.root?.message;

  return (
    <form noValidate onSubmit={(e) => void save(e)} className="space-y-4">
      {problem && (
        <Alert tone="danger" title="Couldn't save the test">
          {problemMessage(problem)}
        </Alert>
      )}
      <Field id="t-title" label="Title" error={err("title")}>
        <Input {...describedBy("t-title", err("title"))} {...register("title")} placeholder="e.g. Week 6 test" autoComplete="off" />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="t-scope" label="Subject and class" error={err("scope")} hint={scopeLocked ? "Fixed now the test has sections." : undefined}>
          {scopeLocked && test ? (
            <Input {...describedBy("t-scope", err("scope"), true)} value={`${test.subject} · ${test.classLevel}`} readOnly />
          ) : (
            <Select
              {...describedBy("t-scope", err("scope"), scopeLocked)}
              {...register("scope", {
                onChange: (e: { target: { value: string } }) => {
                  const level = parseScope(e.target.value).classLevelId;
                  setValue("classRoomIds", getValues("classRoomIds").filter((id) => arms.find((a) => a.id === id)?.classLevelId === level));
                },
              })}
            >
              <option value="">Choose…</option>
              {scopeOptions.map((s) => (
                <option key={scopeKey(s.subjectId, s.classLevelId)} value={scopeKey(s.subjectId, s.classLevelId)}>
                  {s.subject} · {s.classLevel}
                </option>
              ))}
            </Select>
          )}
        </Field>
        <Field id="t-term" label="Term" error={err("termId")}>
          <Select {...describedBy("t-term", err("termId"))} {...register("termId")}>
            {terms.map((t) => (
              <option key={t.id} value={t.id}>
                {t.session} · {t.label}
                {t.isCurrent ? " (current)" : ""}
              </option>
            ))}
          </Select>
        </Field>
        <Field id="t-component" label="Counts towards" error={err("componentId")} hint="Scores are scaled to the component's maximum when they go to the gradebook.">
          <Select {...describedBy("t-component", err("componentId"), true)} {...register("componentId")}>
            <option value="">Nothing (practice test)</option>
            {components.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name} (out of {c.maxScore})
              </option>
            ))}
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-4">
          <Field id="t-duration" label="Length (minutes)" error={err("durationMinutes")}>
            <Input {...describedBy("t-duration", err("durationMinutes"))} {...register("durationMinutes")} inputMode="numeric" className="tabular" />
          </Field>
          <Field id="t-attempts" label="Attempts" error={err("maxAttempts")}>
            <Input {...describedBy("t-attempts", err("maxAttempts"))} {...register("maxAttempts")} inputMode="numeric" className="tabular" />
          </Field>
        </div>
      </div>

      <fieldset aria-describedby={armsError ? "t-arms-error" : "t-arms-hint"}>
        <legend className="mb-1.5 text-sm font-medium">Classes that sit it</legend>
        {levelArms.length === 0 ? (
          <p id="t-arms-hint" className="text-sm text-muted">
            {scope ? "No classes at this level yet." : "Choose the subject and class first."}
          </p>
        ) : (
          <>
            <div className="flex flex-wrap gap-2">
              {levelArms.map((a) => (
                <label key={a.id} className="flex min-h-11 cursor-pointer items-center gap-2 rounded-input border border-border px-3 text-sm has-[:checked]:border-brand has-[:checked]:bg-brand/5">
                  <input
                    type="checkbox"
                    checked={chosen.includes(a.id)}
                    onChange={(e) =>
                      setValue("classRoomIds", e.target.checked ? [...chosen, a.id] : chosen.filter((id) => id !== a.id), { shouldDirty: true, shouldValidate: Boolean(errors.classRoomIds) })
                    }
                    className="size-4 accent-[var(--brand)]"
                  />
                  {a.name}
                </label>
              ))}
            </div>
            <p id="t-arms-hint" className="mt-1 text-xs text-muted">
              {chosen.length} chosen. You can only choose classes you teach this subject in, or are the form teacher of.
            </p>
          </>
        )}
        {armsError && (
          <p id="t-arms-error" className="mt-1 text-sm text-danger" role="alert">
            {armsError}
          </p>
        )}
      </fieldset>

      <fieldset>
        <legend className="mb-1.5 text-sm font-medium">Window (Abuja time, WAT)</legend>
        <p className="mb-2 text-xs text-muted">Students can start any time in the window; it must be at least as long as the sitting.</p>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="grid grid-cols-2 gap-2">
            <Field id="t-opens-date" label="Opening date" error={err("opensDate")}>
              <Input type="date" {...describedBy("t-opens-date", err("opensDate"))} {...register("opensDate")} />
            </Field>
            <Field id="t-opens-time" label="Opening time" error={err("opensTime")}>
              <Input type="time" {...describedBy("t-opens-time", err("opensTime"))} {...register("opensTime")} />
            </Field>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <Field id="t-closes-date" label="Closing date" error={err("closesDate")}>
              <Input type="date" {...describedBy("t-closes-date", err("closesDate"))} {...register("closesDate")} />
            </Field>
            <Field id="t-closes-time" label="Closing time" error={err("closesTime")}>
              <Input type="time" {...describedBy("t-closes-time", err("closesTime"))} {...register("closesTime")} />
            </Field>
          </div>
        </div>
      </fieldset>

      <div className="flex flex-wrap gap-x-8 gap-y-3">
        {(
          [
            ["shuffleQuestions", "Shuffle the questions"],
            ["shuffleOptions", "Shuffle the options"],
          ] as const
        ).map(([name, label]) => (
          <Controller
            key={name}
            control={control}
            name={name}
            render={({ field }) => (
              <span className="flex items-center gap-3 text-sm">
                <Switch checked={field.value} onCheckedChange={field.onChange} label={label} />
                <span aria-hidden>{label}</span>
              </span>
            )}
          />
        ))}
      </div>

      <Field id="t-instructions" label="Instructions for students (optional)" error={err("instructions")}>
        <textarea {...describedBy("t-instructions", err("instructions"))} {...register("instructions")} rows={3} className={textareaClass} />
      </Field>

      <div className="flex justify-end">
        <Button type="submit" loading={isSubmitting} loadingText="Saving…">
          <Save className="size-4" aria-hidden />
          {test ? "Save details" : "Create and add sections"}
        </Button>
      </div>
    </form>
  );
}
