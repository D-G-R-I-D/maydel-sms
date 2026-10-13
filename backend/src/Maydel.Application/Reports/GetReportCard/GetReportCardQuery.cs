using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.GetReportCard;

/// <summary>A student's report card for a term: a live preview while draft, frozen once published (E5.4).</summary>
public sealed record GetReportCardQuery(Guid StudentId, Guid TermId) : IQuery<ReportCard>;
