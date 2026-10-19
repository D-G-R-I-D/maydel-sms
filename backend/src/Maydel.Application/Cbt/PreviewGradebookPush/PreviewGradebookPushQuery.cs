using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.PreviewGradebookPush;

/// <summary>What sending the results to the grade sheet would do: each student's raw and scaled score, current value, and whether it can go in.</summary>
public sealed record PreviewGradebookPushQuery(Guid TestId) : IQuery<GradebookPreview>;
