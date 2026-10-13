using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Common.Access;

namespace Maydel.Application.Reports.GetReportCard;

internal sealed class GetReportCardHandler(AccessRules access, ReportBuilder reports) : IQueryHandler<GetReportCardQuery, ReportCard>
{
    public async Task<Result<ReportCard>> Handle(GetReportCardQuery q, CancellationToken ct)
    {
        var classRoomId = await reports.ReportClassAsync(q.StudentId, q.TermId, ct);
        if (classRoomId is null) return ReportErrors.NoClass;
        if (!await access.TeachesClassAsync(classRoomId.Value, ct)) return ReportErrors.NotAllowed;
        return await reports.BuildAsync(q.StudentId, q.TermId, classRoomId.Value, ct) is { } card ? card : ReportErrors.StudentNotFound;
    }
}
