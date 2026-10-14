using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Portal.GetChildResult;

/// <summary>A published report card for the parent's child or the signed-in student. Drafts are 404 <c>report.not_published</c>.</summary>
public sealed record GetChildResultQuery(Guid StudentId, Guid TermId) : IQuery<ReportCard>;
