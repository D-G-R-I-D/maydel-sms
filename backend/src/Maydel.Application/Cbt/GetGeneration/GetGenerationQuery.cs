using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetGeneration;

/// <summary>The teacher's own AI request with every version.</summary>
public sealed record GetGenerationQuery(Guid Id) : IQuery<AiGenerationDto>;
