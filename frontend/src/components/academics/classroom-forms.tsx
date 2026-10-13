"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Pencil, Plus, UserRound } from "lucide-react";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { ClassLevelDto, ClassRoomDto } from "@/lib/api/types";
import { createClassroom, updateClassroom } from "@/lib/academics/actions";
import { runAction } from "@/lib/client/action";
import { SECTION_ORDER, classroomSchema, sectionLabels, type ClassroomFormValues, type ClassroomInput } from "@/lib/validation/academics";
import { Button, Field, Input, Select, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";
import { CancelButton, DialogActions, FormDialog } from "@/components/ui/dialog";

const fields = ["classLevelId", "arm", "capacity", "formTeacherId"] as const;

/** Disabled until the staff list arrives (M1.1 part 2): shows the current teacher, sends it unchanged. */
export function TeacherPicker({ id, label, current }: { id: string; label: string; current: string | null }) {
  return (
    <Field id={id} label={label} hint="Staff list coming soon. Teachers can be assigned once staff records are in.">
      <div className="relative">
        <UserRound className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted" aria-hidden />
        <Select id={id} aria-describedby={`${id}-hint`} disabled className="pl-9">
          <option>{current ?? "Not assigned"}</option>
        </Select>
      </div>
    </Field>
  );
}

function ClassroomForm({ levels, classroom, close }: { levels: ClassLevelDto[]; classroom?: ClassRoomDto; close: () => void }) {
  const [problem, setProblem] = useState<Problem | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ClassroomFormValues, unknown, ClassroomInput>({
    resolver: zodResolver(classroomSchema(classroom?.studentCount ?? 1)),
    defaultValues: { classLevelId: classroom?.classLevelId ?? "", arm: classroom?.arm ?? "", capacity: classroom?.capacity ?? 30 },
  });

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    const saved = await runAction(
      () =>
        classroom
          ? updateClassroom(classroom.id, { arm: values.arm, capacity: values.capacity, formTeacherId: classroom.formTeacherId })
          : createClassroom({ classLevelId: values.classLevelId, arm: values.arm, capacity: values.capacity }),
      {
        success: (c) => (classroom ? `${c.name} updated` : `${c.name} created`),
        fields,
        setError: (f, e) => setError(f === "formTeacherId" ? "arm" : f, e),
        onProblem: (p) => (p.code === "classroom.duplicate" ? setError("arm", { type: "server", message: problemMessage(p) }) : setProblem(p)),
      },
    );
    if (saved) close();
  });

  const bySection = SECTION_ORDER.map((section) => [section, levels.filter((l) => l.section === section)] as const).filter(([, items]) => items.length > 0);

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      {problem && problem.status > 0 && problem.status < 500 && problem.status !== 429 && (
        <Alert tone="danger" className="animate-rise" title="Couldn't save">
          <p>{problemMessage(problem)}</p>
        </Alert>
      )}
      {classroom ? (
        <div className="flex items-center justify-between rounded-input bg-surface-2 px-3 py-2 text-sm">
          <span className="font-medium">{classroom.classLevel}</span>
          <span className="text-muted">The year group can&apos;t change</span>
        </div>
      ) : (
        <Field id="class-level" label="Year group" error={errors.classLevelId?.message}>
          <Select {...describedBy("class-level", errors.classLevelId?.message)} {...register("classLevelId")}>
            <option value="">Choose…</option>
            {bySection.map(([section, items]) => (
              <optgroup key={section} label={sectionLabels[section]}>
                {items.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.name}
                  </option>
                ))}
              </optgroup>
            ))}
          </Select>
        </Field>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="class-arm" label="Arm" error={errors.arm?.message} hint="e.g. A, B, Gold">
          <Input {...describedBy("class-arm", errors.arm?.message, true)} {...register("arm")} maxLength={32} autoComplete="off" />
        </Field>
        <Field
          id="class-capacity"
          label="Capacity"
          error={errors.capacity?.message}
          hint={classroom && classroom.studentCount > 0 ? `${classroom.studentCount} students enrolled now.` : "Up to 200."}
        >
          <Input {...describedBy("class-capacity", errors.capacity?.message, true)} {...register("capacity")} type="number" inputMode="numeric" min={Math.max(1, classroom?.studentCount ?? 1)} max={200} />
        </Field>
      </div>
      <TeacherPicker id="class-form-teacher" label="Form teacher" current={classroom?.formTeacher ?? null} />
      <DialogActions>
        <CancelButton />
        <Button type="submit" loading={isSubmitting}>
          {classroom ? "Save changes" : "Create class"}
        </Button>
      </DialogActions>
    </form>
  );
}

export function NewClassroomButton({ levels }: { levels: ClassLevelDto[] }) {
  return (
    <FormDialog
      title="New class"
      description="A class is a year group plus an arm, e.g. Year 3 B."
      trigger={
        <Button>
          <Plus className="size-4" aria-hidden />
          New class
        </Button>
      }
    >
      {(close) => <ClassroomForm levels={levels} close={close} />}
    </FormDialog>
  );
}

export function EditClassroomButton({ levels, classroom }: { levels: ClassLevelDto[]; classroom: ClassRoomDto }) {
  return (
    <FormDialog
      title={`Edit ${classroom.name}`}
      trigger={
        <Button variant="ghost" size="sm" aria-label={`Edit ${classroom.name}`}>
          <Pencil className="size-4" aria-hidden />
        </Button>
      }
    >
      {(close) => <ClassroomForm levels={levels} classroom={classroom} close={close} />}
    </FormDialog>
  );
}
