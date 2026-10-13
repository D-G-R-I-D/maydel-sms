using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.ListSessions;

/// <summary>All sessions, newest first, each with its terms in order.</summary>
public sealed record ListSessionsQuery : IQuery<IReadOnlyList<SessionDto>>;
