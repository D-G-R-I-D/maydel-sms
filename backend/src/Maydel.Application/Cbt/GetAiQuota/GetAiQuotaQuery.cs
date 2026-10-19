using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetAiQuota;

/// <summary>The caller's AI allowance this month: shown before generating (E16.9).</summary>
public sealed record GetAiQuotaQuery : IQuery<AiQuotaDto>;
