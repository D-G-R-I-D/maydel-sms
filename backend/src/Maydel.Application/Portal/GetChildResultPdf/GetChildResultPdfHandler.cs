using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Portal.GetChildResultPdf;

internal sealed class GetChildResultPdfHandler(PortalReports portal, ReportPdfs pdfs) : IQueryHandler<GetChildResultPdfQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetChildResultPdfQuery q, CancellationToken ct)
    {
        var card = await portal.PublishedCardAsync(q.StudentId, q.TermId, ct);
        if (card.IsFailure) return card.Error!;
        return new PdfFile(await pdfs.RenderAsync([card.Value], ct), ReportPdfs.FileName(card.Value));
    }
}
