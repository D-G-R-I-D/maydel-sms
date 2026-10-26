import { describe, expect, it } from "vitest";
import { checkHomeworkFile, classChoices, dueLabel, fileSize, formToHomeworkInput, homeworkErrors, homeworkToForm } from "./homework";

describe("homework helpers", () => {
  it("groups a teacher's classes with their subjects", () => {
    const rows = [
      { classRoomId: "c2", classRoom: "Year 4 A", levelOrder: 4, subjectId: "s1", subject: "Maths", isFormClass: false },
      { classRoomId: "c1", classRoom: "Year 3 A", levelOrder: 3, subjectId: "s2", subject: "English", isFormClass: true },
      { classRoomId: "c1", classRoom: "Year 3 A", levelOrder: 3, subjectId: "s1", subject: "Maths", isFormClass: false },
    ];
    const c = classChoices(rows);
    expect(c.map((x) => x.classRoom)).toEqual(["Year 3 A", "Year 4 A"]);
    expect(c[0]).toMatchObject({ isFormClass: true, subjects: [{ name: "English" }, { name: "Maths" }] });
  });

  it("says when it's due", () => {
    expect(dueLabel("2026-10-26", "2026-10-26")).toBe("Due today");
    expect(dueLabel("2026-10-27", "2026-10-26")).toBe("Due tomorrow");
    expect(dueLabel("2026-10-30", "2026-10-26")).toMatch(/^Due /);
    expect(dueLabel("2026-10-20", "2026-10-26")).toMatch(/^Was due /);
  });

  it("checks files and sizes like the server", () => {
    expect(checkHomeworkFile({ name: "work.pdf", size: 1000 })).toBeNull();
    expect(checkHomeworkFile({ name: "virus.exe", size: 10 })).toMatch(/PDF/);
    expect(checkHomeworkFile({ name: "big.jpg", size: 11 * 1024 * 1024 })).toMatch(/10 MB/);
    expect(fileSize(1536)).toBe("2 KB");
    expect(fileSize(3 * 1024 * 1024)).toBe("3 MB");
  });

  it("validates and builds the input", () => {
    const f = { ...homeworkToForm(null, { dueOn: "2026-10-28" }), classRoomId: "c1", title: " Fractions " };
    expect(homeworkErrors(f, "2026-10-26", false)).toEqual({});
    expect(homeworkErrors({ ...f, dueOn: "2026-10-25" }, "2026-10-26", false).dueOn).toBeDefined();
    expect(homeworkErrors({ ...f, dueOn: "2026-10-25" }, "2026-10-26", true).dueOn).toBeUndefined();
    expect(formToHomeworkInput({ ...f, instructionsHtml: "<p></p>" })).toMatchObject({ title: "Fractions", subjectId: null, instructionsHtml: null });
  });
});
