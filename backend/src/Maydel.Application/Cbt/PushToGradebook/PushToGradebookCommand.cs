using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.PushToGradebook;

/// <summary>
/// After the teacher confirms the preview, writes the scaled scores into the grade sheet column (E16.5): all ready students, or
/// just <c>StudentIds</c>. Students with a published report, not in the class, or not taught by the caller are skipped.
/// </summary>
public sealed record PushToGradebookCommand(Guid TestId, IReadOnlyList<Guid>? StudentIds) : ICommand<PushResult>;
