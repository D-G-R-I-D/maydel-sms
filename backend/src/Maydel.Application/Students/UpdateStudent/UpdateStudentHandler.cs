using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students.UpdateStudent;

internal sealed class UpdateStudentHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<UpdateStudentCommand, StudentDetail>
{
    public async Task<Result<StudentDetail>> Handle(UpdateStudentCommand c, CancellationToken ct)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == c.Id, ct);
        if (student is null) return StudentErrors.NotFound;

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (Student.ValidateDates(c.DateOfBirth, c.AdmittedOn, today) is { } invalid) return invalid.ToError();
        if (c.ClassRoomId is { } classRoomId && classRoomId != student.ClassRoomId && student.IsActive
            && await db.CheckClassHasRoomAsync(classRoomId, student.Id, ct) is { } full) return full;

        student.FirstName = c.FirstName.Trim();
        student.MiddleName = Clean(c.MiddleName);
        student.LastName = c.LastName.Trim();
        student.DateOfBirth = c.DateOfBirth;
        student.Sex = c.Sex;
        student.StateOfOrigin = Clean(c.StateOfOrigin);
        student.Lga = Clean(c.Lga);
        student.HomeAddress = Clean(c.HomeAddress);
        student.House = Clean(c.House);
        student.MedicalNotes = Clean(c.MedicalNotes);
        student.ClassRoomId = c.ClassRoomId;
        student.AdmittedOn = c.AdmittedOn;

        await db.SaveChangesAsync(ct);
        return (await db.GetStudentDetailAsync(student.Id, ct))!;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
