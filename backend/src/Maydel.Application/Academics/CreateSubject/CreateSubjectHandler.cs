using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Academics;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.CreateSubject;

internal sealed class CreateSubjectHandler(IAppDbContext db) : ICommandHandler<CreateSubjectCommand, SubjectDto>
{
    public async Task<Result<SubjectDto>> Handle(CreateSubjectCommand c, CancellationToken ct)
    {
        var (name, code) = (c.Name.Trim(), Subject.NormalizeCode(c.Code));
        if (await db.Subjects.AnyAsync(s => s.Name.ToLower() == name.ToLower() || s.Code == code, ct)) return AcademicsErrors.DuplicateSubject;

        var subject = new Subject { Name = name, Code = code, IsActive = c.IsActive };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(ct);
        return new SubjectDto(subject.Id, subject.Name, subject.Code, subject.IsActive);
    }
}
