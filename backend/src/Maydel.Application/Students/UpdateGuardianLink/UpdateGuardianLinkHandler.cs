using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students.UpdateGuardianLink;

internal sealed class UpdateGuardianLinkHandler(IAppDbContext db) : ICommandHandler<UpdateGuardianLinkCommand, StudentDetail>
{
    public async Task<Result<StudentDetail>> Handle(UpdateGuardianLinkCommand c, CancellationToken ct)
    {
        var links = await db.StudentGuardians.Where(sg => sg.StudentId == c.StudentId).ToListAsync(ct);
        if (links.Count == 0 && !await db.Students.AnyAsync(s => s.Id == c.StudentId, ct)) return StudentErrors.NotFound;
        var link = links.FirstOrDefault(l => l.GuardianId == c.GuardianId);
        if (link is null) return StudentErrors.NotLinked;

        if (c.IsPrimaryContact)
            foreach (var other in links.Where(l => l != link && l.IsPrimaryContact)) other.IsPrimaryContact = false;
        (link.Relationship, link.IsPrimaryContact, link.CanPickUp) = (c.Relationship, c.IsPrimaryContact, c.CanPickUp);

        await db.SaveChangesAsync(ct);
        return (await db.GetStudentDetailAsync(c.StudentId, ct))!;
    }
}
