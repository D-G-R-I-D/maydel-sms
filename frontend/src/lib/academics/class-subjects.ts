import type { ClassSubjectDto, SubjectDto } from "@/lib/api/types";

/**
 * Subjects shown in a class's checklist: every active subject, plus inactive ones the class
 * already offers (they stay, but inactive subjects can't be newly added). Sorted by name.
 */
export function editableSubjects(all: readonly SubjectDto[], assigned: readonly ClassSubjectDto[]): SubjectDto[] {
  const offered = new Set(assigned.map((a) => a.subjectId));
  return all.filter((s) => s.isActive || offered.has(s.id)).sort((a, b) => a.name.localeCompare(b.name));
}

export function diffSelection(saved: readonly string[], selected: ReadonlySet<string>) {
  const before = new Set(saved);
  const added = new Set([...selected].filter((id) => !before.has(id)));
  const removed = new Set([...before].filter((id) => !selected.has(id)));
  return { added, removed, dirty: added.size > 0 || removed.size > 0 };
}
