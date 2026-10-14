"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { problemMessage, type Problem } from "@/lib/api/problem";
import type { ClassRoomDto, StudentDetail } from "@/lib/api/types";
import { admitStudent, updateStudent } from "@/lib/students/actions";
import { runAction } from "@/lib/client/action";
import { groupBySection } from "@/lib/validation/academics";
import { SEXES, schoolToday, studentSchema, type StudentFormValues, type StudentPayload } from "@/lib/validation/students";
import { Button, Field, Input, Select, cn, describedBy } from "@/components/ui/primitives";
import { Alert } from "@/components/ui/states";

const fields = ["firstName", "middleName", "lastName", "dateOfBirth", "sex", "admittedOn", "classRoomId", "stateOfOrigin", "lga", "homeAddress", "house", "medicalNotes"] as const;

/** Class picker: grouped by section, shows students/capacity, full classes disabled (except the current one). */
function ClassOptions({ classrooms, currentId }: { classrooms: ClassRoomDto[]; currentId: string | null }) {
  return (
    <>
      <option value="">No class yet</option>
      {groupBySection(classrooms).map((g) => (
        <optgroup key={g.section} label={g.label}>
          {g.rows.map((c) => {
            const full = c.studentCount >= c.capacity && c.id !== currentId;
            return (
              <option key={c.id} value={c.id} disabled={full}>
                {c.name} — {c.studentCount}/{c.capacity}
                {full ? " (full)" : ""}
              </option>
            );
          })}
        </optgroup>
      ))}
    </>
  );
}

function Section({ title, children, className }: { title: string; children: React.ReactNode; className?: string }) {
  return (
    <fieldset className={cn("space-y-4", className)}>
      <legend className="mb-3 text-xs font-semibold uppercase tracking-wider text-muted">{title}</legend>
      {children}
    </fieldset>
  );
}

/** Admit a student (then open their profile) or edit biodata. The admission number never changes. */
export function StudentForm({
  classrooms,
  student,
  onSaved,
  onCancel,
}: {
  classrooms: ClassRoomDto[];
  student?: StudentDetail;
  onSaved?: (s: StudentDetail) => void;
  onCancel?: () => void;
}) {
  const router = useRouter();
  const [problem, setProblem] = useState<Problem | null>(null);
  const today = schoolToday();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<StudentFormValues, unknown, StudentPayload>({
    resolver: zodResolver(studentSchema(today)),
    defaultValues: {
      firstName: student?.firstName ?? "",
      middleName: student?.middleName ?? "",
      lastName: student?.lastName ?? "",
      dateOfBirth: student?.dateOfBirth ?? "",
      sex: student?.sex ?? "",
      stateOfOrigin: student?.stateOfOrigin ?? "",
      lga: student?.lga ?? "",
      homeAddress: student?.homeAddress ?? "",
      house: student?.house ?? "",
      medicalNotes: student?.medicalNotes ?? "",
      classRoomId: student?.classRoomId ?? "",
      admittedOn: student?.admittedOn ?? today,
    },
  });

  const onSubmit = handleSubmit(async (values) => {
    setProblem(null);
    const saved = await runAction(() => (student ? updateStudent(student.id, values) : admitStudent(values)), {
      success: (s) => (student ? `${s.fullName} saved` : `${s.fullName} admitted as ${s.admissionNumber}`),
      fields,
      setError,
      onProblem: (p) => (p.code === "classroom.full" ? setError("classRoomId", { type: "server", message: problemMessage(p) }, { shouldFocus: true }) : setProblem(p)),
    });
    if (!saved) return;
    if (student) onSaved?.(saved);
    else router.push(`/admin/students/${saved.id}`);
  });

  const text = (name: (typeof fields)[number], label: string, extra: React.InputHTMLAttributes<HTMLInputElement> = {}, className?: string) => (
    <Field id={`st-${name}`} label={label} error={errors[name]?.message} className={className}>
      <Input {...describedBy(`st-${name}`, errors[name]?.message)} {...register(name)} autoComplete="off" {...extra} />
    </Field>
  );

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-8">
      {problem && problem.status > 0 && problem.status !== 429 && problem.status < 500 && (
        <Alert tone="danger" className="animate-rise" title="Couldn't save">
          <p>{problemMessage(problem)}</p>
        </Alert>
      )}

      <Section title="Name">
        <div className="grid gap-4 sm:grid-cols-3">
          {text("firstName", "First name")}
          {text("middleName", "Middle name (optional)")}
          {text("lastName", "Surname")}
        </div>
      </Section>

      <Section title="Enrolment">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field id="st-classRoomId" label="Class" error={errors.classRoomId?.message} hint="Full classes can't take more students.">
            <Select {...describedBy("st-classRoomId", errors.classRoomId?.message, true)} {...register("classRoomId")}>
              <ClassOptions classrooms={classrooms} currentId={student?.classRoomId ?? null} />
            </Select>
          </Field>
          <Field
            id="st-admittedOn"
            label="Admitted on"
            error={errors.admittedOn?.message}
            hint={student ? `Admission number ${student.admissionNumber} doesn't change.` : "Sets the year in the admission number."}
          >
            <Input {...describedBy("st-admittedOn", errors.admittedOn?.message, true)} {...register("admittedOn")} type="date" readOnly={Boolean(student)} className={student ? "bg-surface-2 text-muted" : undefined} />
          </Field>
        </div>
      </Section>

      <Section title="Biodata">
        <div className="grid gap-4 sm:grid-cols-2">
          {text("dateOfBirth", "Date of birth (optional)", { type: "date", max: today })}
          <Field id="st-sex" label="Sex (optional)" error={errors.sex?.message}>
            <Select {...describedBy("st-sex", errors.sex?.message)} {...register("sex")}>
              <option value="">Not recorded</option>
              {SEXES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </Select>
          </Field>
          {text("stateOfOrigin", "State of origin (optional)")}
          {text("lga", "LGA (optional)")}
          {text("house", "House (optional)", { placeholder: "e.g. Blue House" })}
          {text("homeAddress", "Home address (optional)", {}, "sm:col-span-2")}
        </div>
        <Field id="st-medicalNotes" label="Medical notes (optional)" error={errors.medicalNotes?.message} hint="Allergies, conditions or medication staff should know about.">
          <textarea
            {...describedBy("st-medicalNotes", errors.medicalNotes?.message, true)}
            {...register("medicalNotes")}
            rows={3}
            className="block w-full rounded-input border border-border bg-surface px-3 py-2 text-base text-text placeholder:text-muted focus-visible:outline-2 focus-visible:outline-brand sm:text-sm"
          />
        </Field>
      </Section>

      <div className="flex flex-col-reverse gap-2 border-t border-border pt-4 sm:flex-row sm:justify-end">
        {onCancel && (
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
        )}
        <Button type="submit" loading={isSubmitting} loadingText="Saving…">
          {student ? "Save changes" : "Admit student"}
        </Button>
      </div>
    </form>
  );
}
