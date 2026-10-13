using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.UnpublishClassReports;

/// <summary>Returns published reports to draft so scores, comments and attendance can be corrected (E5.2). All students, or just <c>StudentIds</c>.</summary>
public sealed record UnpublishClassReportsCommand(Guid ClassRoomId, Guid TermId, IReadOnlyList<Guid>? StudentIds) : ICommand<UnpublishResult>;
