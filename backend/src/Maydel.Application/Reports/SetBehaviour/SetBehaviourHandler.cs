using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Reports.SetBehaviour;

internal sealed class SetBehaviourHandler(IAppDbContext db, AccessRules access, ReportBuilder reports) : ICommandHandler<SetBehaviourCommand, ReportCard>
{
    public async Task<Result<ReportCard>> Handle(SetBehaviourCommand c, CancellationToken ct)
    {
        if (!await db.Terms.AnyAsync(t => t.Id == c.TermId, ct)) return ReportErrors.TermNotFound;
        var classRoomId = await reports.ReportClassAsync(c.StudentId, c.TermId, ct);
        if (classRoomId is null) return ReportErrors.NoClass;
        if (!await access.IsFormTeacherAsync(classRoomId.Value, ct)) return ReportErrors.NotFormTeacher;

        var traitIds = c.Ratings.Select(r => r.TraitId).Distinct().ToList();
        if (await db.BehaviourTraits.CountAsync(t => traitIds.Contains(t.Id), ct) != traitIds.Count)
            return Error.Validation("ratings", "One of the traits no longer exists. Reload and try again.");

        var report = await reports.GetOrCreateAsync(c.StudentId, c.TermId, classRoomId.Value, ct);
        if (report.SetBehaviour([.. c.Ratings.Select(r => (r.TraitId, r.Rating))], [.. c.Clubs.Select(x => ((string?)x.Name, x.Rating))]) is { } invalid)
            return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return (await reports.BuildAsync(c.StudentId, c.TermId, classRoomId.Value, ct))!;
    }
}
