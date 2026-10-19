using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.ReleaseResults;

/// <summary>Shows the scores to students and parents (E16.4). Needs the window closed, nobody still sitting, and everything marked.</summary>
public sealed record ReleaseResultsCommand(Guid TestId) : ICommand<TestDetail>;
