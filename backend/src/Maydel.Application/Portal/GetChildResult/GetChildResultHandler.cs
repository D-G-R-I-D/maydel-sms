using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Reports;

namespace Maydel.Application.Portal.GetChildResult;

internal sealed class GetChildResultHandler(PortalReports portal) : IQueryHandler<GetChildResultQuery, ReportCard>
{
    public async Task<Result<ReportCard>> Handle(GetChildResultQuery q, CancellationToken ct) =>
        await portal.PublishedCardAsync(q.StudentId, q.TermId, ct);
}
