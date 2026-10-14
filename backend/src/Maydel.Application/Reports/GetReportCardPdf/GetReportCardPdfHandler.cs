using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Reports.GetReportCardPdf;

internal sealed class GetReportCardPdfHandler(AccessRules access, ReportBuilder reports, ReportPdfs pdfs) : IQueryHandler<GetReportCardPdfQuery, PdfFile>
{
    public async Task<Result<PdfFile>> Handle(GetReportCardPdfQuery q, CancellationToken ct)
    {
        var classRoomId = await reports.ReportClassAsync(q.StudentId, q.TermId, ct);
        if (classRoomId is null) return ReportErrors.NoClass;
        if (!await access.TeachesClassAsync(classRoomId.Value, ct)) return ReportErrors.NotAllowed;
        if (await reports.BuildAsync(q.StudentId, q.TermId, classRoomId.Value, ct) is not { } card) return ReportErrors.StudentNotFound;

        return new PdfFile(await pdfs.RenderAsync([card], ct), ReportPdfs.FileName(card));
    }
}
