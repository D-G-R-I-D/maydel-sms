using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.GetFeeSettings;

internal sealed class GetFeeSettingsHandler(IAppDbContext db) : IQueryHandler<GetFeeSettingsQuery, FeeSettingsDto>
{
    public async Task<Result<FeeSettingsDto>> Handle(GetFeeSettingsQuery q, CancellationToken ct)
    {
        var s = await db.SettingsAsync(ct);
        return new FeeSettingsDto(s.SiblingMode, s.SecondChild, s.ThirdAndLater);
    }
}
