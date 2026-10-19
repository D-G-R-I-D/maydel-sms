using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.ListGenerations;

/// <summary>The teacher's recent AI requests, newest first (last 50).</summary>
public sealed record ListGenerationsQuery : IQuery<IReadOnlyList<AiGenerationSummary>>;
