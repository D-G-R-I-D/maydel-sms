using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Domain.Academics;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Promotions;

/// <summary>
/// The end-of-session promotion screen (E3.6) for one class or all of them. <c>CanPromote</c> is false until the session's third
/// term starts; <c>ClosedReason</c> says why.
/// </summary>
public sealed record PromotionPlan(Guid SessionId, string Session, bool CanPromote, string? ClosedReason, IReadOnlyList<PromotionClass> Classes);

/// <summary>
/// A class and its students. <c>NextClassRoomId</c> is where the class moves up to by default (the next level's same arm, or its
/// only class); null with <c>IsFinalLevel</c> false means the school must choose. Students in the top level graduate by default.
/// </summary>
public sealed record PromotionClass(
    Guid ClassRoomId, string Name, int LevelOrder, bool IsFinalLevel, Guid? NextClassRoomId, string? NextClassName,
    IReadOnlyList<PromotionStudent> Students);

/// <summary>
/// One student. <c>SessionAverage</c> is the mean of their published term reports this session; <c>NeedsReview</c> flags an average
/// below 40 (a hint only). <c>Done</c> is set once they've been moved on this session; otherwise the suggestion applies.
/// </summary>
public sealed record PromotionStudent(
    Guid StudentId, string FullName, string AdmissionNumber, StudentStatus Status, decimal? SessionAverage, bool NeedsReview,
    PromotionOutcome SuggestedOutcome, Guid? SuggestedClassRoomId, PromotionDone? Done);

public sealed record PromotionDone(PromotionOutcome Outcome, Guid ToClassRoomId, string ToClassName, DateTimeOffset At);

/// <summary>One decision. <c>ToClassRoomId</c> is required for Promote and ignored otherwise.</summary>
public sealed record PromotionDecision(Guid StudentId, PromotionOutcome Outcome, Guid? ToClassRoomId);

public sealed record PromotionSummary(int Promoted, int Repeated, int Graduated, int Withdrawn);

/// <summary><c>Skipped</c>: students changed by hand since the run (moved or status changed), left as they are.</summary>
public sealed record PromotionUndone(int Undone, IReadOnlyList<string> Skipped);

public static class PromotionErrors
{
    public static readonly Error NoSession = Error.NotFound("promotion.no_session", "That session doesn't exist, or no current session is set.");
    public static readonly Error ClassNotFound = Error.NotFound("classroom.not_found", "That class doesn't exist.");
    public static Error AlreadyDone(string name, string session) => Error.Conflict("promotion.already_done",
        $"{name} has already been moved on for {session}. Undo that class's promotion first to change it.");
    public static Error ChooseClass(string name) => Error.Rule("promotion.choose_class", $"Choose the class {name} moves up to.");
    public static Error SameClass(string name) => Error.Rule("promotion.same_class", $"{name} would stay in the same class. Choose Repeat instead.");
    public static Error NotEnrolled(string name) => Error.Rule("promotion.not_enrolled", $"{name} isn't an active student in a class.");
    public static Error ClassFull(string name, int count, int capacity) => Error.Rule("promotion.class_full",
        $"{name} would have {count} students (capacity {capacity}). Promote the class above it in the same run, raise its capacity, or choose another class.");
    public static Error NoRoomToUndo(string name, int count, int capacity) => Error.Rule("promotion.undo_class_full",
        $"{name} would have {count} students again (capacity {capacity}). Undo the class that moved into it first, or raise its capacity.");
    public static readonly Error NothingToUndo = Error.NotFound("promotion.nothing_to_undo", "Nobody from that class has been moved on this session.");
}

internal static class PromotionQueries
{
    /// <summary>The given session, or the current one, with its terms.</summary>
    public static Task<AcademicSession?> PromotionSessionAsync(this IAppDbContext db, Guid? sessionId, CancellationToken ct) =>
        db.Sessions.AsNoTracking().Include(s => s.Terms)
            .Where(s => sessionId == null ? s.IsCurrent : s.Id == sessionId).FirstOrDefaultAsync(ct);

    /// <summary>Every class with its level, for names, capacities and finding the next level.</summary>
    public static async Task<Dictionary<Guid, ClassInfo>> ClassInfosAsync(this IAppDbContext db, CancellationToken ct) =>
        await db.ClassRooms.AsNoTracking()
            .Select(r => new ClassInfo(r.Id, r.ClassLevel.Name + " " + r.Arm, r.Arm, r.ClassLevel.Order, r.Capacity))
            .ToDictionaryAsync(r => r.Id, ct);

    /// <summary>
    /// Active students each class would have after the changes tracked on <paramref name="changed"/>; returns the first class over
    /// capacity, if any.
    /// </summary>
    public static async Task<(ClassInfo Class, int Count)?> OverfullAsync(
        this IAppDbContext db, IEnumerable<Guid> classIds, IReadOnlyCollection<Student> changed, Dictionary<Guid, ClassInfo> classes, CancellationToken ct)
    {
        var ids = changed.Select(s => s.Id).ToList();
        foreach (var classId in classIds.Distinct())
        {
            var others = await db.Students.CountAsync(s => s.ClassRoomId == classId && s.Status == StudentStatus.Active && !ids.Contains(s.Id), ct);
            var count = others + changed.Count(s => s.ClassRoomId == classId && s.IsActive);
            if (count > classes[classId].Capacity) return (classes[classId], count);
        }
        return null;
    }
}

internal sealed record ClassInfo(Guid Id, string Name, string Arm, int LevelOrder, int Capacity);
