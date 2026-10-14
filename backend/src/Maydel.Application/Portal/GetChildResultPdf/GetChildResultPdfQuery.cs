using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Portal.GetChildResultPdf;

/// <summary>The PDF of a published report card for the parent's child or the signed-in student.</summary>
public sealed record GetChildResultPdfQuery(Guid StudentId, Guid TermId) : IQuery<PdfFile>;
