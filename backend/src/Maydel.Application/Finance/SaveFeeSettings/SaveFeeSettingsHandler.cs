using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.SaveFeeSettings;

internal sealed class SaveFeeSettingsHandler(IAppDbContext db) : ICommandHandler<SaveFeeSettingsCommand, FeeSettingsDto>
{
    public async Task<Result<FeeSettingsDto>> Handle(SaveFeeSettingsCommand c, CancellationToken ct)
    {
        if (!Enum.IsDefined(c.SiblingMode)) return Error.Validation("siblingMode", "Choose None, Percentage or FixedAmount.");
        var (second, third) = c.SiblingMode == SiblingDiscountMode.None ? (0m, 0m) : (c.SecondChild, c.ThirdAndLater);
        if (FeeRules.CheckSiblingSettings(c.SiblingMode, second, third) is { } invalid) return invalid.ToError();

        var settings = await db.FeeSettings.OrderBy(s => s.CreatedAt).FirstOrDefaultAsync(ct);
        if (settings is null) db.FeeSettings.Add(settings = new FeeSettings());
        (settings.SiblingMode, settings.SecondChild, settings.ThirdAndLater) = (c.SiblingMode, second, third);
        await db.SaveChangesAsync(ct);
        return new FeeSettingsDto(settings.SiblingMode, settings.SecondChild, settings.ThirdAndLater);
    }
}
