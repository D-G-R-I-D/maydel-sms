using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Assessment.ListGradableClasses;

/// <summary>
/// The class/subject pairs the caller can enter scores for: a teacher's assigned subjects plus every subject in classes
/// they form-teach; an admin gets every subject offered anywhere. Drives the teacher's "My classes" screen.
/// </summary>
public sealed record ListGradableClassesQuery : IQuery<IReadOnlyList<GradableClass>>;
