using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Common.Access;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports.SetFormTeacherComment;

internal sealed class SetFormTeacherCommentHandler(IAppDbContext db, AccessRules access, ReportBuilder reports) : ICommandHandler<SetFormTeacherCommentCommand, ReportCard>
{
    public async Task<Result<ReportCard>> Handle(SetFormTeacherCommentCommand c, CancellationToken ct)
    {
        if (!await db.Terms.AnyAsync(t => t.Id == c.TermId, ct)) return ReportErrors.TermNotFound;
        var classRoomId = await reports.ReportClassAsync(c.StudentId, c.TermId, ct);
        if (classRoomId is null) return ReportErrors.NoClass;
        if (!await access.IsFormTeacherAsync(classRoomId.Value, ct)) return ReportErrors.NotFormTeacher;
        var report = await reports.GetOrCreateAsync(c.StudentId, c.TermId, classRoomId.Value, ct);
        if (report.SetFormTeacherComment(c.Comment) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return (await reports.BuildAsync(c.StudentId, c.TermId, classRoomId.Value, ct))!;
    }
}
