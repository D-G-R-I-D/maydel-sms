using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Notifications;
using Maydel.Application.Website;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Maydel.Domain.Homework;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Homework;

public sealed record HomeworkInput(Guid ClassRoomId, Guid? SubjectId, string Title, string? Instructions, string? InstructionsHtml, DateOnly DueOn,
    bool AcceptsSubmissions = true);

public sealed record HomeworkFileDto(Guid Id, string FileName, string ContentType, long Size);

/// <summary><c>Counts</c> (staff only): hand-ins so far out of the class size.</summary>
public sealed record HomeworkDto(Guid Id, Guid ClassRoomId, string ClassRoom, Guid? SubjectId, string? Subject, string Title, string Instructions,
    string? InstructionsHtml, DateOnly DueOn, bool AcceptsSubmissions, string SetBy, IReadOnlyList<HomeworkFileDto> Files, DateTimeOffset CreatedAt,
    int? Submitted, int? ClassSize);

public enum HandInState
{
    /// <summary>Not handed in yet (or the homework doesn't take hand-ins).</summary>
    Missing,
    Submitted,
    Seen,
    Returned,
}

public sealed record SubmissionDto(Guid Id, string? Text, IReadOnlyList<HomeworkFileDto> Files, DateTimeOffset SubmittedAt, bool IsLate, SubmissionStatus Status,
    string? Feedback, DateTimeOffset? ReviewedAt);

/// <summary>A row of the teacher's hand-in list: every active student in the class, handed in or not.</summary>
public sealed record RosterRow(Guid StudentId, string Name, string AdmissionNumber, HandInState State, SubmissionDto? Submission);

/// <summary>Homework as a family sees it, with the child's own hand-in.</summary>
public sealed record MyHomework(HomeworkDto Homework, HandInState State, bool IsOverdue, SubmissionDto? Submission);

public sealed record ReviewInput(SubmissionStatus Status, string? Feedback);

public static class HomeworkErrors
{
    public static readonly Error NotFound = Error.NotFound("homework.not_found", "That homework doesn't exist.");
    public static readonly Error NotYours = Error.Forbidden("homework.forbidden", "You can only set homework for classes and subjects you teach.");
    public static readonly Error HasSubmissions = Error.Rule("homework.has_submissions", "Students have handed this in, so it can't be deleted. Edit it instead.");
    public static readonly Error ClosedForSubmissions = Error.Rule("homework.no_submissions", "This homework isn't handed in online.");
    public static readonly Error TooManyFiles = Error.Rule("homework.too_many_files", "Attach up to 5 files.");
    public static readonly Error SubmissionNotFound = Error.NotFound("homework.submission_not_found", "That hand-in doesn't exist.");
    public static readonly Error EmptySubmission = Error.Validation("text", "Write your answer or attach a file.");
    public static readonly Error OnlyStudents = Error.Forbidden("homework.students_only", "Only the student can hand in homework.");
}

internal static class HomeworkQueries
{
    public static HomeworkFileDto Dto(this HomeworkFile f) => new(f.Id, f.FileName, f.ContentType, f.Size);

    public static SubmissionDto Dto(this HomeworkSubmission s) =>
        new(s.Id, s.Text, [.. s.Files.Select(f => f.Dto())], s.SubmittedAt, s.IsLate, s.Status, s.Feedback, s.ReviewedAt);

    public static HandInState State(this HomeworkSubmission? s) => s?.Status switch
    {
        null => HandInState.Missing,
        SubmissionStatus.Seen => HandInState.Seen,
        SubmissionStatus.Returned => HandInState.Returned,
        _ => HandInState.Submitted,
    };

    public static async Task<IReadOnlyList<HomeworkDto>> DtosAsync(this IAppDbContext db, IReadOnlyList<HomeworkTask> items, bool withCounts, CancellationToken ct)
    {
        var roomIds = items.Select(h => h.ClassRoomId).Distinct().ToList();
        var subjectIds = items.Where(h => h.SubjectId != null).Select(h => h.SubjectId!.Value).Distinct().ToList();
        var rooms = await db.ClassRooms.AsNoTracking().Where(r => roomIds.Contains(r.Id)).Select(r => new { r.Id, Name = r.ClassLevel.Name + " " + r.Arm }).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var subjects = await db.Subjects.AsNoTracking().Where(s => subjectIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        Dictionary<Guid, int> sizes = [], handedIn = [];
        if (withCounts)
        {
            sizes = await db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active && s.ClassRoomId != null && roomIds.Contains(s.ClassRoomId.Value))
                .GroupBy(s => s.ClassRoomId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            var ids = items.Select(h => h.Id).ToList();
            handedIn = await db.HomeworkSubmissions.AsNoTracking().Where(s => ids.Contains(s.HomeworkId))
                .GroupBy(s => s.HomeworkId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        }
        return [.. items.Select(h => new HomeworkDto(h.Id, h.ClassRoomId, rooms.GetValueOrDefault(h.ClassRoomId) ?? "", h.SubjectId,
            h.SubjectId is { } sid ? subjects.GetValueOrDefault(sid) : null, h.Title, h.Instructions, h.InstructionsHtml, h.DueOn, h.AcceptsSubmissions, h.SetByName,
            [.. h.Files.Select(f => f.Dto())], h.CreatedAt, withCounts ? handedIn.GetValueOrDefault(h.Id) : null, withCounts ? sizes.GetValueOrDefault(h.ClassRoomId) : null))];
    }

    /// <summary>Staff who may manage this homework: admins, the class's form teacher, or the subject's teacher.</summary>
    public static async Task<bool> CanManageAsync(this AccessRules access, Guid classRoomId, Guid? subjectId, CancellationToken ct) =>
        subjectId is { } sid ? await access.CanGradeAsync(classRoomId, sid, ct) : await access.IsFormTeacherAsync(classRoomId, ct);
}

// ---------- Staff ----------

public sealed record ListHomeworkQuery(Guid? ClassRoomId = null, Guid? SubjectId = null, int? Page = null, int? PageSize = null) : IQuery<PagedResult<HomeworkDto>>;
public sealed record GetHomeworkQuery(Guid Id) : IQuery<HomeworkDto>;
public sealed record SaveHomeworkCommand(Guid? Id, HomeworkInput Input) : ICommand<HomeworkDto>;
public sealed record DeleteHomeworkCommand(Guid Id) : ICommand;
public sealed record AddHomeworkFileCommand(Guid Id, byte[] File, string? FileName) : ICommand<HomeworkDto>;
public sealed record RemoveHomeworkFileCommand(Guid Id, Guid FileId) : ICommand<HomeworkDto>;
public sealed record ListHandInsQuery(Guid Id) : IQuery<IReadOnlyList<RosterRow>>;
public sealed record ReviewHandInCommand(Guid Id, Guid SubmissionId, ReviewInput Input) : ICommand<SubmissionDto>;

internal sealed class ListHomeworkHandler(IAppDbContext db, ICurrentUser me) : IQueryHandler<ListHomeworkQuery, PagedResult<HomeworkDto>>
{
    public async Task<Result<PagedResult<HomeworkDto>>> Handle(ListHomeworkQuery q, CancellationToken ct)
    {
        var list = db.Homework.AsNoTracking();
        if (!me.IsInRole(Roles.Admin))
        {
            // Teachers see homework for classes they form-teach or teach a subject in.
            var uid = me.UserId!.Value;
            var mine = db.ClassRooms.Where(c => (c.FormTeacher != null && c.FormTeacher.UserId == uid) || c.Subjects.Any(cs => cs.Teacher != null && cs.Teacher.UserId == uid)).Select(c => c.Id);
            list = list.Where(h => mine.Contains(h.ClassRoomId));
        }
        if (q.ClassRoomId is { } room) list = list.Where(h => h.ClassRoomId == room);
        if (q.SubjectId is { } subject) list = list.Where(h => h.SubjectId == subject);
        var page = await list.OrderByDescending(h => h.DueOn).ThenByDescending(h => h.CreatedAt).ToPagedAsync(q.Page, q.PageSize, ct);
        return new PagedResult<HomeworkDto>([.. await db.DtosAsync(page.Items, withCounts: true, ct)], page.Page, page.PageSize, page.TotalCount);
    }
}

internal sealed class GetHomeworkHandler(IAppDbContext db, AccessRules access) : IQueryHandler<GetHomeworkQuery, HomeworkDto>
{
    public async Task<Result<HomeworkDto>> Handle(GetHomeworkQuery q, CancellationToken ct)
    {
        var h = await db.Homework.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (h is null || !await access.TeachesClassAsync(h.ClassRoomId, ct)) return HomeworkErrors.NotFound;
        return (await db.DtosAsync([h], withCounts: true, ct))[0];
    }
}

internal sealed class SaveHomeworkHandler(IAppDbContext db, AccessRules access, ICurrentUser me, ISchoolClock clock, HomeworkNotices notices)
    : ICommandHandler<SaveHomeworkCommand, HomeworkDto>
{
    public async Task<Result<HomeworkDto>> Handle(SaveHomeworkCommand c, CancellationToken ct)
    {
        var i = c.Input;
        if (!await db.ClassRooms.AnyAsync(r => r.Id == i.ClassRoomId, ct)) return Error.Validation("classRoomId", "Choose a class.");
        if (i.SubjectId is { } sid && !await db.ClassSubjects.AnyAsync(cs => cs.ClassRoomId == i.ClassRoomId && cs.SubjectId == sid, ct))
            return Error.Validation("subjectId", "That subject isn't taught in this class.");
        if (!await access.CanManageAsync(i.ClassRoomId, i.SubjectId, ct)) return HomeworkErrors.NotYours;
        if (i.InstructionsHtml is { Length: > RichText.MaxLength }) return Error.Validation("instructionsHtml", "The instructions are too long.");

        HomeworkTask h;
        var isNew = c.Id is null;
        if (c.Id is { } id)
        {
            if (await db.Homework.FirstOrDefaultAsync(x => x.Id == id, ct) is not { } found || !await access.CanManageAsync(found.ClassRoomId, found.SubjectId, ct))
                return HomeworkErrors.NotFound;
            h = found;
        }
        else
        {
            if (i.DueOn < clock.Today) return Error.Validation("dueOn", "The due date can't be in the past.");
            var name = await db.Staff.AsNoTracking().Where(s => s.UserId == me.UserId).Select(s => s.FullName).FirstOrDefaultAsync(ct) ?? me.UserName ?? "Teacher";
            db.Homework.Add(h = new HomeworkTask { Title = "", SetByUserId = me.UserId!.Value, SetByName = name });
        }
        (h.ClassRoomId, h.SubjectId, h.Title, h.DueOn, h.AcceptsSubmissions) = (i.ClassRoomId, i.SubjectId, i.Title ?? "", i.DueOn, i.AcceptsSubmissions);
        if (RichText.Clean(i.InstructionsHtml) is { } rich) (h.Instructions, h.InstructionsHtml) = (rich.Text, rich.Html);
        else (h.Instructions, h.InstructionsHtml) = ((i.Instructions ?? "").Trim(), null);
        if (h.Check() is { } invalid) return invalid.ToError();
        if (isNew) await notices.SetAsync(h, ct);
        await db.SaveChangesAsync(ct);
        return (await db.DtosAsync([h], withCounts: true, ct))[0];
    }
}

internal sealed class DeleteHomeworkHandler(IAppDbContext db, AccessRules access, HomeworkFiles files) : ICommandHandler<DeleteHomeworkCommand>
{
    public async Task<Result> Handle(DeleteHomeworkCommand c, CancellationToken ct)
    {
        if (await db.Homework.FirstOrDefaultAsync(x => x.Id == c.Id, ct) is not { } h || !await access.CanManageAsync(h.ClassRoomId, h.SubjectId, ct))
            return HomeworkErrors.NotFound;
        if (await db.HomeworkSubmissions.AnyAsync(s => s.HomeworkId == h.Id, ct)) return HomeworkErrors.HasSubmissions;
        db.Homework.Remove(h);
        await db.SaveChangesAsync(ct);
        foreach (var f in h.Files) await files.DeleteAsync(f, ct);
        return Result.Success();
    }
}

internal sealed class AddHomeworkFileHandler(IAppDbContext db, AccessRules access, HomeworkFiles files) : ICommandHandler<AddHomeworkFileCommand, HomeworkDto>
{
    public async Task<Result<HomeworkDto>> Handle(AddHomeworkFileCommand c, CancellationToken ct)
    {
        if (await db.Homework.FirstOrDefaultAsync(x => x.Id == c.Id, ct) is not { } h || !await access.CanManageAsync(h.ClassRoomId, h.SubjectId, ct))
            return HomeworkErrors.NotFound;
        if (h.Files.Count >= HomeworkTask.MaxFiles) return HomeworkErrors.TooManyFiles;
        if (await files.SaveAsync(c.File, c.FileName, "homework", ct) is not { } file) return HomeworkFiles.Invalid;
        h.Files.Add(file);
        await db.SaveChangesAsync(ct);
        return (await db.DtosAsync([h], withCounts: true, ct))[0];
    }
}

internal sealed class RemoveHomeworkFileHandler(IAppDbContext db, AccessRules access, HomeworkFiles files) : ICommandHandler<RemoveHomeworkFileCommand, HomeworkDto>
{
    public async Task<Result<HomeworkDto>> Handle(RemoveHomeworkFileCommand c, CancellationToken ct)
    {
        if (await db.Homework.FirstOrDefaultAsync(x => x.Id == c.Id, ct) is not { } h || !await access.CanManageAsync(h.ClassRoomId, h.SubjectId, ct))
            return HomeworkErrors.NotFound;
        if (h.Files.FirstOrDefault(f => f.Id == c.FileId) is { } file)
        {
            h.Files.Remove(file);
            await db.SaveChangesAsync(ct);
            await files.DeleteAsync(file, ct);
        }
        return (await db.DtosAsync([h], withCounts: true, ct))[0];
    }
}

internal sealed class ListHandInsHandler(IAppDbContext db, AccessRules access) : IQueryHandler<ListHandInsQuery, IReadOnlyList<RosterRow>>
{
    public async Task<Result<IReadOnlyList<RosterRow>>> Handle(ListHandInsQuery q, CancellationToken ct)
    {
        var h = await db.Homework.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (h is null || !await access.TeachesClassAsync(h.ClassRoomId, ct)) return HomeworkErrors.NotFound;
        var students = await db.Students.AsNoTracking().Where(s => s.ClassRoomId == h.ClassRoomId && s.Status == StudentStatus.Active)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName).Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, s.AdmissionNumber }).ToListAsync(ct);
        var handIns = await db.HomeworkSubmissions.AsNoTracking().Where(s => s.HomeworkId == h.Id).ToDictionaryAsync(s => s.StudentId, ct);
        return students.Select(s =>
        {
            var sub = handIns.GetValueOrDefault(s.Id);
            return new RosterRow(s.Id, s.Name, s.AdmissionNumber, sub.State(), sub?.Dto());
        }).ToList();
    }
}

internal sealed class ReviewHandInHandler(IAppDbContext db, AccessRules access, ICurrentUser me, ISchoolClock clock, HomeworkNotices notices)
    : ICommandHandler<ReviewHandInCommand, SubmissionDto>
{
    public async Task<Result<SubmissionDto>> Handle(ReviewHandInCommand c, CancellationToken ct)
    {
        var h = await db.Homework.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.Id, ct);
        if (h is null || !await access.CanManageAsync(h.ClassRoomId, h.SubjectId, ct)) return HomeworkErrors.NotFound;
        if (await db.HomeworkSubmissions.FirstOrDefaultAsync(s => s.Id == c.SubmissionId && s.HomeworkId == h.Id, ct) is not { } s) return HomeworkErrors.SubmissionNotFound;
        if (c.Input.Status is not (SubmissionStatus.Seen or SubmissionStatus.Returned)) return Error.Validation("status", "Mark it Seen or Returned.");
        var feedback = string.IsNullOrWhiteSpace(c.Input.Feedback) ? null : c.Input.Feedback.Trim();
        if (feedback is { Length: > 4000 }) return Error.Validation("feedback", "Keep feedback under 4,000 characters.");
        (s.Status, s.Feedback, s.ReviewedAt, s.ReviewedByUserId) = (c.Input.Status, feedback, clock.Now.ToUniversalTime(), me.UserId);
        await notices.ReviewedAsync(h, s, ct);
        await db.SaveChangesAsync(ct);
        return s.Dto();
    }
}

// ---------- Portal (students and parents) ----------

public sealed record ListChildHomeworkQuery(Guid StudentId, bool? Open = null) : IQuery<IReadOnlyList<MyHomework>>;
public sealed record GetChildHomeworkQuery(Guid StudentId, Guid HomeworkId) : IQuery<MyHomework>;

/// <summary>The student hands in (or again, replacing the earlier hand-in): text and up to 5 files.</summary>
public sealed record HandInCommand(Guid StudentId, Guid HomeworkId, string? Text, IReadOnlyList<(byte[] Bytes, string? Name)> Files) : ICommand<MyHomework>;

internal sealed class PortalHomework(IAppDbContext db, AccessRules access, ISchoolClock clock)
{
    public async Task<(Student Student, IQueryable<HomeworkTask> Homework)?> ForAsync(Guid studentId, CancellationToken ct)
    {
        if (await access.PortalStudents().AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct) is not { ClassRoomId: { } room } student) return null;
        return (student, db.Homework.AsNoTracking().Where(h => h.ClassRoomId == room));
    }

    public async Task<IReadOnlyList<MyHomework>> ViewAsync(Guid studentId, IReadOnlyList<HomeworkTask> items, CancellationToken ct)
    {
        var ids = items.Select(h => h.Id).ToList();
        var mine = await db.HomeworkSubmissions.AsNoTracking().Where(s => s.StudentId == studentId && ids.Contains(s.HomeworkId)).ToDictionaryAsync(s => s.HomeworkId, ct);
        var dtos = await db.DtosAsync(items, withCounts: false, ct);
        return [.. items.Select((h, n) =>
        {
            var sub = mine.GetValueOrDefault(h.Id);
            return new MyHomework(dtos[n], sub.State(), h.AcceptsSubmissions && sub is null && h.DueOn < clock.Today, sub?.Dto());
        })];
    }
}

internal sealed class ListChildHomeworkHandler(PortalHomework portal, ISchoolClock clock) : IQueryHandler<ListChildHomeworkQuery, IReadOnlyList<MyHomework>>
{
    public async Task<Result<IReadOnlyList<MyHomework>>> Handle(ListChildHomeworkQuery q, CancellationToken ct)
    {
        if (await portal.ForAsync(q.StudentId, ct) is not { } found) return Error.NotFound("portal.student_not_found", "That student isn't linked to your account.");
        var list = found.Homework;
        // Open = due today or later; otherwise the last 60 days as well.
        list = q.Open == true ? list.Where(h => h.DueOn >= clock.Today) : list.Where(h => h.DueOn >= clock.Today.AddDays(-60));
        var items = await list.OrderBy(h => h.DueOn).ThenBy(h => h.CreatedAt).Take(100).ToListAsync(ct);
        return Result<IReadOnlyList<MyHomework>>.Success(await portal.ViewAsync(q.StudentId, items, ct));
    }
}

internal sealed class GetChildHomeworkHandler(PortalHomework portal) : IQueryHandler<GetChildHomeworkQuery, MyHomework>
{
    public async Task<Result<MyHomework>> Handle(GetChildHomeworkQuery q, CancellationToken ct)
    {
        if (await portal.ForAsync(q.StudentId, ct) is not { } found || await found.Homework.FirstOrDefaultAsync(h => h.Id == q.HomeworkId, ct) is not { } h)
            return HomeworkErrors.NotFound;
        return (await portal.ViewAsync(q.StudentId, [h], ct))[0];
    }
}

internal sealed class HandInHandler(IAppDbContext db, PortalHomework portal, ICurrentUser me, ISchoolClock clock, HomeworkFiles files)
    : ICommandHandler<HandInCommand, MyHomework>
{
    public async Task<Result<MyHomework>> Handle(HandInCommand c, CancellationToken ct)
    {
        if (await portal.ForAsync(c.StudentId, ct) is not { } found || await found.Homework.FirstOrDefaultAsync(h => h.Id == c.HomeworkId, ct) is not { } h)
            return HomeworkErrors.NotFound;
        if (!me.IsInRole(Roles.Student) || found.Student.UserId != me.UserId) return HomeworkErrors.OnlyStudents;
        if (!h.AcceptsSubmissions) return HomeworkErrors.ClosedForSubmissions;
        var text = string.IsNullOrWhiteSpace(c.Text) ? null : c.Text.Trim();
        if (text is { Length: > 20000 }) return Error.Validation("text", "Keep your answer under 20,000 characters.");
        if (c.Files.Count > HomeworkSubmission.MaxFiles) return HomeworkErrors.TooManyFiles;
        if (text is null && c.Files.Count == 0) return HomeworkErrors.EmptySubmission;

        var saved = new List<HomeworkFile>();
        foreach (var (bytes, name) in c.Files)
        {
            if (await files.SaveAsync(bytes, name, "handins", ct) is not { } file)
            {
                foreach (var s in saved) await files.DeleteAsync(s, ct);
                return HomeworkFiles.Invalid;
            }
            saved.Add(file);
        }

        var now = clock.Now.ToUniversalTime();
        var sub = await db.HomeworkSubmissions.FirstOrDefaultAsync(s => s.HomeworkId == h.Id && s.StudentId == c.StudentId, ct);
        var replaced = sub?.Files.ToList() ?? [];
        if (sub is null) db.HomeworkSubmissions.Add(sub = new HomeworkSubmission { HomeworkId = h.Id, StudentId = c.StudentId });
        (sub.Text, sub.Files, sub.SubmittedAt, sub.IsLate, sub.Status, sub.Feedback, sub.ReviewedAt, sub.ReviewedByUserId) =
            (text, saved, now, clock.Today > h.DueOn, SubmissionStatus.Submitted, null, null, null);
        await db.SaveChangesAsync(ct);
        foreach (var old in replaced) await files.DeleteAsync(old, ct);
        return (await portal.ViewAsync(c.StudentId, [h], ct))[0];
    }
}

// ---------- Files ----------

/// <summary>A homework attachment: staff who teach the class, or the class's students and their families.</summary>
public sealed record GetHomeworkFileQuery(Guid HomeworkId, Guid FileId) : IQuery<HomeworkFileContent>;

/// <summary>A hand-in file: staff who teach the class, the student, and the student's family.</summary>
public sealed record GetHandInFileQuery(Guid HomeworkId, Guid SubmissionId, Guid FileId) : IQuery<HomeworkFileContent>;

internal sealed class GetHomeworkFileHandler(IAppDbContext db, AccessRules access, HomeworkFiles files) : IQueryHandler<GetHomeworkFileQuery, HomeworkFileContent>
{
    public async Task<Result<HomeworkFileContent>> Handle(GetHomeworkFileQuery q, CancellationToken ct)
    {
        var h = await db.Homework.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.HomeworkId, ct);
        if (h is null || h.Files.FirstOrDefault(f => f.Id == q.FileId) is not { } file) return HomeworkErrors.NotFound;
        var allowed = await access.TeachesClassAsync(h.ClassRoomId, ct) || await access.PortalStudents().AnyAsync(s => s.ClassRoomId == h.ClassRoomId, ct);
        return allowed && await files.OpenAsync(file, ct) is { } content ? content : HomeworkErrors.NotFound;
    }
}

internal sealed class GetHandInFileHandler(IAppDbContext db, AccessRules access, HomeworkFiles files) : IQueryHandler<GetHandInFileQuery, HomeworkFileContent>
{
    public async Task<Result<HomeworkFileContent>> Handle(GetHandInFileQuery q, CancellationToken ct)
    {
        var h = await db.Homework.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.HomeworkId, ct);
        var sub = await db.HomeworkSubmissions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == q.SubmissionId && s.HomeworkId == q.HomeworkId, ct);
        if (h is null || sub is null || sub.Files.FirstOrDefault(f => f.Id == q.FileId) is not { } file) return HomeworkErrors.NotFound;
        var allowed = await access.TeachesClassAsync(h.ClassRoomId, ct) || await access.PortalStudents().AnyAsync(s => s.Id == sub.StudentId, ct);
        return allowed && await files.OpenAsync(file, ct) is { } content ? content : HomeworkErrors.NotFound;
    }
}

// ---------- Notifications ----------

/// <summary>Tells students (with a login) and their guardians about new homework and reviewed hand-ins.</summary>
public sealed class HomeworkNotices(IAppDbContext db, Notifier notifier)
{
    public const string PortalHomework = "/portal/homework";

    public async Task SetAsync(HomeworkTask h, CancellationToken ct)
    {
        var subject = h.SubjectId is { } sid ? await db.Subjects.AsNoTracking().Where(s => s.Id == sid).Select(s => s.Name).FirstOrDefaultAsync(ct) : null;
        var students = await db.Students.AsNoTracking().Where(s => s.ClassRoomId == h.ClassRoomId && s.Status == StudentStatus.Active)
            .Select(s => new { s.Id, s.FirstName, s.UserId }).ToListAsync(ct);
        var due = h.DueOn.ToString("ddd d MMM", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var s in students)
        {
            var title = $"New {(subject is null ? "" : subject + " ")}homework for {s.FirstName}";
            var body = $"{h.Title} – due {due}.";
            var link = $"{PortalHomework}/{h.Id}";
            await notifier.GuardiansAsync(s.Id, NotificationKind.HomeworkSet, title, body, link, $"homework:{h.Id}", ct);
            if (s.UserId is { } uid) notifier.User(uid, s.Id, NotificationKind.HomeworkSet, $"New {(subject is null ? "" : subject + " ")}homework", body, link, $"homework:{h.Id}");
        }
    }

    public async Task ReviewedAsync(HomeworkTask h, HomeworkSubmission s, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking().Where(x => x.Id == s.StudentId).Select(x => new { x.FirstName, x.UserId }).FirstAsync(ct);
        var what = s.Status == SubmissionStatus.Returned ? "returned with feedback" : "seen by the teacher";
        var key = $"handin:{s.Id}:{s.Status}:{s.ReviewedAt:O}";
        var link = $"{PortalHomework}/{h.Id}";
        await notifier.GuardiansAsync(s.StudentId, NotificationKind.HomeworkReviewed, $"{student.FirstName}'s homework was {what}", h.Title, link, key, ct);
        if (student.UserId is { } uid) notifier.User(uid, s.StudentId, NotificationKind.HomeworkReviewed, $"Your homework was {what}", h.Title, link, key);
    }
}
