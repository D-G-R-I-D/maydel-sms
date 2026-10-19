using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.ListMyTests;

/// <summary>The student's online tests: open now first, then upcoming, then finished (E16.3).</summary>
public sealed record ListMyTestsQuery : IQuery<IReadOnlyList<StudentTestDto>>;
