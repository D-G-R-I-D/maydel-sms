using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.ListSubjects;

/// <summary>The subject catalogue, active and inactive, by name.</summary>
public sealed record ListSubjectsQuery : IQuery<IReadOnlyList<SubjectDto>>;
