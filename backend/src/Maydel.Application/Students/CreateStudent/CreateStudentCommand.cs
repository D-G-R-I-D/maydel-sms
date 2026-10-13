using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;

namespace Maydel.Application.Students.CreateStudent;

/// <summary>Admits a student (E3.1). The admission number MSS/{admission year}/#### is generated. Logins are issued separately.</summary>
public sealed record CreateStudentCommand(
    string FirstName, string? MiddleName, string LastName, DateOnly? DateOfBirth, Sex? Sex,
    string? StateOfOrigin, string? Lga, string? HomeAddress, string? House, string? MedicalNotes,
    Guid? ClassRoomId, DateOnly AdmittedOn) : ICommand<StudentDetail>;
