using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.SetPrincipalComment;

/// <summary>The principal's comment on a student's report (E5.3). Admins only. Empty clears it. Refused (422) once the report is published.</summary>
public sealed record SetPrincipalCommentCommand(Guid StudentId, Guid TermId, string? Comment) : ICommand<ReportCard>;
