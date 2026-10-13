using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.ListClassLevels;

/// <summary>Year groups in promotion order (Creche to Year 12). Fixed reference data, seeded.</summary>
public sealed record ListClassLevelsQuery : IQuery<IReadOnlyList<ClassLevelDto>>;
