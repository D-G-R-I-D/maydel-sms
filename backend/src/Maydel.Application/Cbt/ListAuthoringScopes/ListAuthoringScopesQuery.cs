using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.ListAuthoringScopes;

/// <summary>Subjects × class levels the caller can write questions for (all active subjects × levels for admins), with bank sizes.</summary>
public sealed record ListAuthoringScopesQuery : IQuery<IReadOnlyList<AuthoringScope>>;
