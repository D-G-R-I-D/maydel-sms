using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports.SetPrincipalComment;

internal sealed class SetPrincipalCommentHandler(IAppDbContext db, ReportBuilder reports) : ICommandHandler<SetPrincipalCommentCommand, ReportCard>
{
    public async Task<Result<ReportCard>> Handle(SetPrincipalCommentCommand c, CancellationToken ct)
    {
        if (!await db.Terms.AnyAsync(t => t.Id == c.TermId, ct)) return ReportErrors.TermNotFound;
        var classRoomId = await reports.ReportClassAsync(c.StudentId, c.TermId, ct);
        if (classRoomId is null) return ReportErrors.NoClass;
        // Admin-only endpoint (the principal signs as an administrator).
        var report = await reports.GetOrCreateAsync(c.StudentId, c.TermId, classRoomId.Value, ct);
        if (report.SetPrincipalComment(c.Comment) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return (await reports.BuildAsync(c.StudentId, c.TermId, classRoomId.Value, ct))!;
    }
}
