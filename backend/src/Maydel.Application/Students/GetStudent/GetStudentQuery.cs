using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Students.GetStudent;

/// <summary>Student profile with guardians. Row-level checked through <c>AccessRules</c>.</summary>
public sealed record GetStudentQuery(Guid Id) : IQuery<StudentDetail>;
