using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Application.Students;
using Maydel.Domain.Academics;
using Maydel.Domain.People;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Promotions.PromoteStudents;

internal sealed class PromoteStudentsHandler(IAppDbContext db, ISchoolClock clock, UserManager<AppUser> users, SessionService sessions)
    : ICommandHandler<PromoteStudentsCommand, PromotionSummary>
{
    public async Task<Result<PromotionSummary>> Handle(PromoteStudentsCommand c, CancellationToken ct)
    {
        var session = await db.PromotionSessionAsync(c.SessionId, ct);
        if (session is null) return PromotionErrors.NoSession;
        if (PromotionRules.CheckOpen(session, clock.Today) is { } closed) return closed.ToError();

        var ids = c.Decisions.Select(d => d.StudentId).ToList();
        var students = await db.Students.Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        if (students.Count != ids.Count) return StudentErrors.NotFound;
        if (await db.Promotions.Where(p => p.SessionId == session.Id && ids.Contains(p.StudentId)).Select(p => p.StudentId).FirstOrDefaultAsync(ct) is var already
            && already != Guid.Empty)
            return PromotionErrors.AlreadyDone(students[already].FullName, session.Name);

        var classes = await db.ClassInfosAsync(ct);
        var targets = new List<Guid>();
        foreach (var d in c.Decisions)
        {
            var student = students[d.StudentId];
            if (student.ClassRoomId is not { } from || student.Status is not (StudentStatus.Active or StudentStatus.Suspended))
                return PromotionErrors.NotEnrolled(student.FullName);
            var previous = student.Status;
            switch (d.Outcome)
            {
                case PromotionOutcome.Promote:
                    if (d.ToClassRoomId is not { } to) return PromotionErrors.ChooseClass(student.FullName);
                    if (!classes.ContainsKey(to)) return PromotionErrors.ClassNotFound;
                    if (to == from) return PromotionErrors.SameClass(student.FullName);
                    student.ClassRoomId = to;
                    targets.Add(to);
                    break;
                case PromotionOutcome.Graduate or PromotionOutcome.Withdraw:
                    var status = d.Outcome == PromotionOutcome.Graduate ? StudentStatus.Graduated : StudentStatus.Withdrawn;
                    if (student.ChangeStatus(status) is { } invalid) return invalid.ToError();
                    break;
            }
            db.Promotions.Add(new PromotionRecord
            {
                SessionId = session.Id, StudentId = student.Id, Outcome = d.Outcome,
                FromClassRoomId = from, ToClassRoomId = student.ClassRoomId!.Value, PreviousStatus = previous,
            });
        }

        // Checked after every move, so Year 3 can move into Year 4 in the same run that moves Year 4 on.
        if (await db.OverfullAsync(targets, students.Values, classes, ct) is { } full)
            return PromotionErrors.ClassFull(full.Class.Name, full.Count, full.Class.Capacity);

        foreach (var student in students.Values.Where(s => !s.IsActive)) await users.SyncStudentLoginAsync(student, sessions, ct);
        await db.SaveChangesAsync(ct);

        int Count(PromotionOutcome o) => c.Decisions.Count(d => d.Outcome == o);
        return new PromotionSummary(Count(PromotionOutcome.Promote), Count(PromotionOutcome.Repeat), Count(PromotionOutcome.Graduate), Count(PromotionOutcome.Withdraw));
    }
}
