using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Assessment.GetBehaviourSettings;

internal sealed class GetBehaviourSettingsHandler(IAppDbContext db) : IQueryHandler<GetBehaviourSettingsQuery, BehaviourSettings>
{
    public async Task<Result<BehaviourSettings>> Handle(GetBehaviourSettingsQuery query, CancellationToken ct) =>
        await db.GetBehaviourSettingsAsync(ct);
}
