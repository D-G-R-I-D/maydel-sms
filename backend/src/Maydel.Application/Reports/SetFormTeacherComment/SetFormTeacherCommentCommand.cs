using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.SetFormTeacherComment;

/// <summary>The form teacher's comment on a student's report (E5.3). Form teacher of the class or an admin. Empty clears it. Refused (422) once the report is published.</summary>
public sealed record SetFormTeacherCommentCommand(Guid StudentId, Guid TermId, string? Comment) : ICommand<ReportCard>;
