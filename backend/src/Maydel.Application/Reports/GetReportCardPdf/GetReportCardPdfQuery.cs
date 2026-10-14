using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.GetReportCardPdf;

/// <summary>The report card as a printable A4 PDF (E5.6). Same access as the on-screen card; drafts are watermarked.</summary>
public sealed record GetReportCardPdfQuery(Guid StudentId, Guid TermId) : IQuery<PdfFile>;
