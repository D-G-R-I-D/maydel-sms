using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Academics;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.UpdateSubject;

internal sealed class UpdateSubjectHandler(IAppDbContext db) : ICommandHandler<UpdateSubjectCommand, SubjectDto>
{
    public async Task<Result<SubjectDto>> Handle(UpdateSubjectCommand c, CancellationToken ct)
    {
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == c.Id, ct);
        if (subject is null) return AcademicsErrors.SubjectNotFound;

        var (name, code) = (c.Name.Trim(), Subject.NormalizeCode(c.Code));
        if (await db.Subjects.AnyAsync(s => s.Id != c.Id && (s.Name.ToLower() == name.ToLower() || s.Code == code), ct)) return AcademicsErrors.DuplicateSubject;

        (subject.Name, subject.Code, subject.IsActive) = (name, code, c.IsActive);
        await db.SaveChangesAsync(ct);
        return new SubjectDto(subject.Id, subject.Name, subject.Code, subject.IsActive);
    }
}
