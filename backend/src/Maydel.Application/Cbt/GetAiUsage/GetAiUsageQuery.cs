using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetAiUsage;

/// <summary>Admin dashboard (E16.9): this month's (or a given month's) AI use per teacher, against allowances and the school cap.</summary>
public sealed record GetAiUsageQuery(int? Year = null, int? Month = null) : IQuery<AiUsage>;

public sealed record GetAiSettingsQuery : IQuery<AiSettingsDto>;
