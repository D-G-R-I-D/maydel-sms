using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Academics;
using Maydel.Domain.Assessment;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Promotions.GetPromotionPlan;

internal sealed class GetPromotionPlanHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<GetPromotionPlanQuery, PromotionPlan>
{
    public async Task<Result<PromotionPlan>> Handle(GetPromotionPlanQuery q, CancellationToken ct)
    {
        var session = await db.PromotionSessionAsync(q.SessionId, ct);
        if (session is null) return PromotionErrors.NoSession;
        var classes = await db.ClassInfosAsync(ct);
        if (q.ClassRoomId is { } only && !classes.ContainsKey(only)) return PromotionErrors.ClassNotFound;

        var students = await db.Students.AsNoTracking()
            .Where(s => s.ClassRoomId != null && (s.Status == StudentStatus.Active || s.Status == StudentStatus.Suspended))
            .Where(s => q.ClassRoomId == null || s.ClassRoomId == q.ClassRoomId)
            .Select(s => new { s.Id, s.FirstName, s.MiddleName, s.LastName, s.AdmissionNumber, s.Status, ClassRoomId = s.ClassRoomId!.Value })
            .ToListAsync(ct);
        var done = await db.Promotions.AsNoTracking()
            .Where(p => p.SessionId == session.Id && (q.ClassRoomId == null || p.FromClassRoomId == q.ClassRoomId))
            .Select(p => new
            {
                p.StudentId, p.Student.FirstName, p.Student.MiddleName, p.Student.LastName, p.Student.AdmissionNumber, p.Student.Status,
                p.FromClassRoomId, p.ToClassRoomId, p.Outcome, p.CreatedAt,
            })
            .ToListAsync(ct);
        var doneIds = done.Select(d => d.StudentId).ToHashSet();
        var studentIds = students.Select(s => s.Id).Concat(doneIds).ToList();
        var averages = await db.TermReports.AsNoTracking()
            .Where(r => r.Term.SessionId == session.Id && r.Status == ReportStatus.Published && studentIds.Contains(r.StudentId))
            .GroupBy(r => r.StudentId).Select(g => new { g.Key, Average = g.Average(r => r.Average) })
            .ToDictionaryAsync(g => g.Key, g => (decimal?)Math.Round(g.Average, 1), ct);

        var classIds = students.Where(s => !doneIds.Contains(s.Id)).Select(s => s.ClassRoomId).Concat(done.Select(d => d.FromClassRoomId)).ToHashSet();
        if (q.ClassRoomId is { } requested) classIds.Add(requested);

        var result = new List<PromotionClass>();
        foreach (var room in classIds.Select(id => classes[id]).OrderBy(c => c.LevelOrder).ThenBy(c => c.Arm))
        {
            var nextOrder = classes.Values.Where(c => c.LevelOrder > room.LevelOrder).Select(c => (int?)c.LevelOrder).Min();
            var isFinal = nextOrder is null;
            var next = isFinal ? null : PromotionRules.DefaultTarget(room.Arm,
                [.. classes.Values.Where(c => c.LevelOrder == nextOrder).Select(c => (c.Id, c.Arm))]);
            var suggested = isFinal ? PromotionOutcome.Graduate : PromotionOutcome.Promote;

            var rows = students.Where(s => s.ClassRoomId == room.Id && !doneIds.Contains(s.Id))
                .Select(s => new PromotionStudent(s.Id, Name(s.FirstName, s.MiddleName, s.LastName), s.AdmissionNumber, s.Status,
                    averages.GetValueOrDefault(s.Id), PromotionRules.NeedsReview(averages.GetValueOrDefault(s.Id)), suggested, next, null))
                .Concat(done.Where(d => d.FromClassRoomId == room.Id)
                    .Select(d => new PromotionStudent(d.StudentId, Name(d.FirstName, d.MiddleName, d.LastName), d.AdmissionNumber, d.Status,
                        averages.GetValueOrDefault(d.StudentId), PromotionRules.NeedsReview(averages.GetValueOrDefault(d.StudentId)), d.Outcome,
                        d.ToClassRoomId, new PromotionDone(d.Outcome, d.ToClassRoomId, classes[d.ToClassRoomId].Name, d.CreatedAt))))
                .OrderBy(s => s.FullName)
                .ToList();
            result.Add(new PromotionClass(room.Id, room.Name, room.LevelOrder, isFinal, next, next is { } n ? classes[n].Name : null, rows));
        }

        var closed = PromotionRules.CheckOpen(session, clock.Today);
        return new PromotionPlan(session.Id, session.Name, closed is null, closed?.Message, result);
    }

    private static string Name(string first, string? middle, string last) =>
        string.Join(' ', new[] { first, middle, last }.Where(p => !string.IsNullOrWhiteSpace(p)));
}
