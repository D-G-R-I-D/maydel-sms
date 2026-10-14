using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Assessment;

namespace Maydel.Application.Assessment.ListGradingPresets;

internal sealed class ListGradingPresetsHandler : IQueryHandler<ListGradingPresetsQuery, IReadOnlyList<GradingPresetDto>>
{
    public Task<Result<IReadOnlyList<GradingPresetDto>>> Handle(ListGradingPresetsQuery query, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<GradingPresetDto>>.Success(
            [.. GradingPresets.All.Select(p => new GradingPresetDto(p.Key, p.Name, p.Description,
                [.. p.Bands.Select(b => new GradeBandDto(b.Grade, b.MinScore, b.MaxScore, b.Remark))]))]));
}
