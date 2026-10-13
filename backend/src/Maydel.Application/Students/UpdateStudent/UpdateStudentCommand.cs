using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;

namespace Maydel.Application.Students.UpdateStudent;

/// <summary>Edits biodata or moves the student to another class. The admission number never changes.</summary>
public sealed record UpdateStudentCommand(
    Guid Id, string FirstName, string? MiddleName, string LastName, DateOnly? DateOfBirth, Sex? Sex,
    string? StateOfOrigin, string? Lga, string? HomeAddress, string? House, string? MedicalNotes,
    Guid? ClassRoomId, DateOnly AdmittedOn) : ICommand<StudentDetail>;
