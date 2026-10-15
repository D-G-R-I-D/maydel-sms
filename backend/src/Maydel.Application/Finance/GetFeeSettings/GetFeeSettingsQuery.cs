using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Finance.GetFeeSettings;

public sealed record GetFeeSettingsQuery : IQuery<FeeSettingsDto>;
