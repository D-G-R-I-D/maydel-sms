using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.SetClassSubjects;

/// <summary>Replaces the subjects offered in a class and their teachers in one call (E2.3, E4.2). A subject that already has scores in this class can't be removed.</summary>
public sealed record SetClassSubjectsCommand(Guid ClassRoomId, IReadOnlyList<ClassSubjectAssignment> Subjects) : ICommand<IReadOnlyList<ClassSubjectDto>>;
