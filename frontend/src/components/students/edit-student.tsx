"use client";

import { Pencil } from "lucide-react";
import type { ClassRoomDto, StudentDetail } from "@/lib/api/types";
import { Button } from "@/components/ui/primitives";
import { FormDialog } from "@/components/ui/dialog";
import { StudentForm } from "./student-form";

export function EditStudentButton({ student, classrooms }: { student: StudentDetail; classrooms: ClassRoomDto[] }) {
  return (
    <FormDialog
      size="lg"
      title={`Edit ${student.fullName}`}
      description={`Admission number ${student.admissionNumber}`}
      trigger={
        <Button variant="secondary">
          <Pencil className="size-4" aria-hidden />
          Edit
        </Button>
      }
    >
      {(close) => <StudentForm student={student} classrooms={classrooms} onSaved={close} onCancel={close} />}
    </FormDialog>
  );
}
