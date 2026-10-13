using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Assessment.GetAssessmentSettings;

internal sealed class GetAssessmentSettingsHandler(IAppDbContext db) : IQueryHandler<GetAssessmentSettingsQuery, AssessmentSettings>
{
    public async Task<Result<AssessmentSettings>> Handle(GetAssessmentSettingsQuery query, CancellationToken ct) =>
        await db.GetSettingsAsync(ct);
}
