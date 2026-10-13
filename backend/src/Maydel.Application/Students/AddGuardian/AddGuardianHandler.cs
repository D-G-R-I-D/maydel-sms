using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students.AddGuardian;

internal sealed class AddGuardianHandler(IAppDbContext db) : ICommandHandler<AddGuardianCommand, GuardianAdded>
{
    public async Task<Result<GuardianAdded>> Handle(AddGuardianCommand c, CancellationToken ct)
    {
        var student = await db.Students.Include(s => s.Guardians).FirstOrDefaultAsync(s => s.Id == c.StudentId, ct);
        if (student is null) return StudentErrors.NotFound;

        var phone = Guardian.NormalizePhone(c.Phone)!;
        var guardian = await db.Guardians.FirstOrDefaultAsync(g => g.Phone == phone, ct);
        var reused = guardian is not null;
        if (guardian is null)
        {
            guardian = new Guardian
            {
                FullName = c.FullName.Trim(),
                Phone = phone,
                Email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email.Trim().ToLowerInvariant(),
                Occupation = string.IsNullOrWhiteSpace(c.Occupation) ? null : c.Occupation.Trim(),
                Address = string.IsNullOrWhiteSpace(c.Address) ? null : c.Address.Trim(),
            };
            db.Guardians.Add(guardian);
        }
        else if (student.Guardians.Any(g => g.GuardianId == guardian.Id))
        {
            return StudentErrors.AlreadyLinked;
        }

        if (c.IsPrimaryContact)
            foreach (var other in student.Guardians.Where(g => g.IsPrimaryContact)) other.IsPrimaryContact = false;

        db.StudentGuardians.Add(new StudentGuardian
        {
            StudentId = student.Id,
            GuardianId = guardian.Id,
            Relationship = c.Relationship,
            // The first guardian is the primary contact unless told otherwise.
            IsPrimaryContact = c.IsPrimaryContact || student.Guardians.Count == 0,
            CanPickUp = c.CanPickUp,
        });

        await db.SaveChangesAsync(ct);
        return new GuardianAdded((await db.GetStudentDetailAsync(student.Id, ct))!, guardian.Id, reused);
    }
}
