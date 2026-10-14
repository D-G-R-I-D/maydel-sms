using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Assessment.ListGradingPresets;

/// <summary>Ready-made grading scales to choose from in Settings → Assessment.</summary>
public sealed record ListGradingPresetsQuery : IQuery<IReadOnlyList<GradingPresetDto>>;
