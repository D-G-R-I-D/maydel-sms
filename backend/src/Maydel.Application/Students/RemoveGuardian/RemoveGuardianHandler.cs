using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Students.RemoveGuardian;

internal sealed class RemoveGuardianHandler(IAppDbContext db) : ICommandHandler<RemoveGuardianCommand>
{
    public async Task<Result> Handle(RemoveGuardianCommand c, CancellationToken ct)
    {
        var link = await db.StudentGuardians.FirstOrDefaultAsync(sg => sg.StudentId == c.StudentId && sg.GuardianId == c.GuardianId, ct);
        if (link is null) return StudentErrors.NotLinked;

        db.StudentGuardians.Remove(link);
        if (link.IsPrimaryContact)
        {
            // Promote another guardian so the student keeps a primary contact.
            var next = await db.StudentGuardians.Where(sg => sg.StudentId == c.StudentId && sg.GuardianId != c.GuardianId)
                .OrderBy(sg => sg.Guardian.FullName).FirstOrDefaultAsync(ct);
            if (next is not null) next.IsPrimaryContact = true;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
