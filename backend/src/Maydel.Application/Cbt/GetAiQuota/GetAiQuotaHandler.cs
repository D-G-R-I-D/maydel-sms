using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetAiQuota;

internal sealed class GetAiQuotaHandler(AiQuestions ai, ICurrentUser me) : IQueryHandler<GetAiQuotaQuery, AiQuotaDto>
{
    public async Task<Result<AiQuotaDto>> Handle(GetAiQuotaQuery q, CancellationToken ct) => await ai.QuotaAsync(me.UserId!.Value, ct);
}
