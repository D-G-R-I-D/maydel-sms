using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Assessment.SaveGradeSheet;

/// <summary>
/// Saves scores for some or all students in a class for one subject and term (E5.1). Only the listed students change,
/// so the grid can autosave just the rows edited. Validated as a whole: nothing is saved if any row is invalid.
/// Students whose report is published can't be changed (E5.2, 422). Returns the refreshed sheet.
/// </summary>
public sealed record SaveGradeSheetCommand(Guid ClassRoomId, Guid SubjectId, Guid TermId, IReadOnlyList<GradeSheetEntry> Entries) : ICommand<GradeSheet>;
