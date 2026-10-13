using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.GetClassReports;

/// <summary>Every active student in the class with their report status and figures for the term (E5.4, E5.5).</summary>
public sealed record GetClassReportsQuery(Guid ClassRoomId, Guid TermId) : IQuery<ClassReportSummary>;
