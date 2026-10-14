using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.GetClassReportCardsPdf;

/// <summary>Every report card in the class for the term as one PDF, one page per student, for printing (E5.6b).</summary>
public sealed record GetClassReportCardsPdfQuery(Guid ClassRoomId, Guid TermId) : IQuery<PdfFile>;
