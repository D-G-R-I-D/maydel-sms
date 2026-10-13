using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.ListClassSubjects;

/// <summary>Subjects offered in a class, with the assigned subject teacher.</summary>
public sealed record ListClassSubjectsQuery(Guid ClassRoomId) : IQuery<IReadOnlyList<ClassSubjectDto>>;
