using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;

namespace Maydel.Application.Students.CreateStudent;

internal sealed class CreateStudentHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<CreateStudentCommand, StudentDetail>
{
    public async Task<Result<StudentDetail>> Handle(CreateStudentCommand c, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (Student.ValidateDates(c.DateOfBirth, c.AdmittedOn, today) is { } invalid) return invalid.ToError();
        if (c.ClassRoomId is { } classRoomId && await db.CheckClassHasRoomAsync(classRoomId, null, ct) is { } full) return full;

        var student = new Student
        {
            AdmissionNumber = await db.NextAdmissionNumberAsync(c.AdmittedOn.Year, ct),
            FirstName = c.FirstName.Trim(),
            MiddleName = Clean(c.MiddleName),
            LastName = c.LastName.Trim(),
            DateOfBirth = c.DateOfBirth,
            Sex = c.Sex,
            StateOfOrigin = Clean(c.StateOfOrigin),
            Lga = Clean(c.Lga),
            HomeAddress = Clean(c.HomeAddress),
            House = Clean(c.House),
            MedicalNotes = Clean(c.MedicalNotes),
            ClassRoomId = c.ClassRoomId,
            AdmittedOn = c.AdmittedOn,
        };
        db.Students.Add(student);
        await db.SaveChangesAsync(ct);
        return (await db.GetStudentDetailAsync(student.Id, ct))!;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
