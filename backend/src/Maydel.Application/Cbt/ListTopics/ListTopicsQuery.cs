using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.ListTopics;

/// <summary>Topics already used in the bank for a subject and level (for tag suggestions), A–Z.</summary>
public sealed record ListTopicsQuery(Guid SubjectId, Guid ClassLevelId) : IQuery<IReadOnlyList<string>>;
