using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Application.Students;
using Maydel.Domain.Academics;
using Maydel.Domain.People;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Promotions.UndoPromotion;

internal sealed class UndoPromotionHandler(IAppDbContext db, UserManager<AppUser> users, SessionService sessions)
    : ICommandHandler<UndoPromotionCommand, PromotionUndone>
{
    public async Task<Result<PromotionUndone>> Handle(UndoPromotionCommand c, CancellationToken ct)
    {
        var session = await db.PromotionSessionAsync(c.SessionId, ct);
        if (session is null) return PromotionErrors.NoSession;
        var records = await db.Promotions.Include(p => p.Student)
            .Where(p => p.SessionId == session.Id && p.FromClassRoomId == c.ClassRoomId).ToListAsync(ct);
        if (records.Count == 0) return PromotionErrors.NothingToUndo;

        var restored = new List<Student>();
        var skipped = new List<string>();
        foreach (var record in records)
        {
            var student = record.Student;
            var statusAfter = record.Outcome switch
            {
                PromotionOutcome.Graduate => StudentStatus.Graduated,
                PromotionOutcome.Withdraw => StudentStatus.Withdrawn,
                _ => record.PreviousStatus,
            };
            if (student.ClassRoomId != record.ToClassRoomId || student.Status != statusAfter)
            {
                skipped.Add(student.FullName); // changed by hand since; its record stays, so it isn't moved on twice
                continue;
            }
            student.ClassRoomId = record.FromClassRoomId;
            student.Status = record.PreviousStatus; // an undo, not a status change: graduating is otherwise final
            db.Promotions.Remove(record);
            restored.Add(student);
        }

        var classes = await db.ClassInfosAsync(ct);
        if (restored.Count > 0 && await db.OverfullAsync([c.ClassRoomId], restored, classes, ct) is { } full)
            return PromotionErrors.NoRoomToUndo(full.Class.Name, full.Count, full.Class.Capacity);

        foreach (var student in restored) await users.SyncStudentLoginAsync(student, sessions, ct);
        await db.SaveChangesAsync(ct);
        return new PromotionUndone(restored.Count, skipped);
    }
}
