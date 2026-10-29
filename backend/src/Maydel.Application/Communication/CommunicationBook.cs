using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Homework;
using Maydel.Application.Notifications;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Communication;

public sealed record BookEntryDto(Guid Id, Guid StudentId, string AuthorName, BookSide Side, bool IsMine, string Body, HomeworkFileDto? Attachment,
    DateTimeOffset CreatedAt, DateTimeOffset? EditedAt, bool CanEdit);

/// <summary>Entries oldest first (like a chat); <c>Before</c> pages back through older ones.</summary>
/// <summary>
/// <c>SeenUpTo</c>: the latest time anyone else opened this book; your notes written before it show as seen.
/// <c>Cursor</c>: pass back as <c>after</c> to get only what's new or corrected since (live updates).
/// </summary>
public sealed record BookPage(Guid StudentId, string Student, string? ClassRoom, IReadOnlyList<BookEntryDto> Entries, bool HasOlder,
    DateTimeOffset? SeenUpTo, DateTimeOffset Cursor);

/// <summary>A row of a book list: the student, the latest note, and how many notes you haven't read.</summary>
public sealed record BookSummary(Guid StudentId, string Name, string AdmissionNumber, string? ClassRoom, DateTimeOffset? LastEntryAt, string? LastEntry, int Unread);

public sealed record EditEntryInput(string Body);

public static class BookErrors
{
    public static readonly Error NotFound = Error.NotFound("book.not_found", "That communication book doesn't exist.");
    public static readonly Error EntryNotFound = Error.NotFound("book.entry_not_found", "That note doesn't exist.");
    public static readonly Error TooLate = Error.Rule("book.edit_window", "Notes can only be corrected in the first 15 minutes. Add a follow-up instead.");
}

/// <summary>
/// Who may open a student's book: admins, the form teacher of the student's class, the student's guardians, and the
/// student. Everyone else gets a 404 (nothing to probe).
/// </summary>
internal sealed class BookAccess(IAppDbContext db, AccessRules access, ICurrentUser me)
{
    public sealed record Opened(Student Student, BookSide Side);

    public async Task<Opened?> OpenAsync(Guid studentId, CancellationToken ct)
    {
        if (await access.PortalStudents().AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct) is { } own) return new(own, BookSide.Family);
        if (!me.IsInRole(Roles.Admin) && !me.IsInRole(Roles.Teacher)) return null;
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null) return null;
        if (me.IsInRole(Roles.Admin) || (student.ClassRoomId is { } room && await access.IsFormTeacherAsync(room, ct))) return new(student, BookSide.School);
        return null;
    }

    public async Task<string> MyNameAsync(CancellationToken ct)
    {
        var uid = me.UserId!.Value;
        return await db.Staff.AsNoTracking().Where(s => s.UserId == uid).Select(s => s.FullName).FirstOrDefaultAsync(ct)
            ?? await db.Guardians.AsNoTracking().Where(g => g.UserId == uid).Select(g => g.FullName).FirstOrDefaultAsync(ct)
            ?? await db.Students.AsNoTracking().Where(s => s.UserId == uid).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync(ct)
            ?? me.UserName ?? "Someone";
    }
}

internal static class BookMapping
{
    public static async Task MarkReadAsync(this IAppDbContext db, Guid studentId, Guid userId, DateTimeOffset at, CancellationToken ct)
    {
        if (await db.BookReads.FirstOrDefaultAsync(r => r.StudentId == studentId && r.UserId == userId, ct) is { } read) read.ReadAt = at > read.ReadAt ? at : read.ReadAt;
        else db.BookReads.Add(new BookRead { StudentId = studentId, UserId = userId, ReadAt = at });
        await db.SaveChangesAsync(ct);
    }

    public static BookEntryDto Dto(this BookEntry e, Guid me, DateTimeOffset now) =>
        new(e.Id, e.StudentId, e.AuthorName, e.Side, e.AuthorUserId == me, e.Body, e.Attachment?.Dto(), e.CreatedAt, e.EditedAt, e.CanEdit(me, now));

    /// <summary>Unread notes per student for this user: notes by others after the user's read mark.</summary>
    public static async Task<Dictionary<Guid, int>> UnreadAsync(this IAppDbContext db, IQueryable<Guid> students, Guid uid, CancellationToken ct) =>
        await db.BookEntries.AsNoTracking()
            .Where(e => students.Contains(e.StudentId) && e.AuthorUserId != uid
                && !db.BookReads.Any(r => r.StudentId == e.StudentId && r.UserId == uid && r.ReadAt >= e.CreatedAt))
            .GroupBy(e => e.StudentId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public static async Task<IReadOnlyList<BookSummary>> SummariesAsync(this IAppDbContext db, IQueryable<Student> students, Guid uid, CancellationToken ct)
    {
        var ids = students.Select(s => s.Id);
        var unread = await db.UnreadAsync(ids, uid, ct);
        var rows = await students.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).Select(s => new
        {
            s.Id, Name = s.FirstName + " " + s.LastName, s.AdmissionNumber,
            Room = s.ClassRoom == null ? null : s.ClassRoom.ClassLevel.Name + " " + s.ClassRoom.Arm,
            Last = db.BookEntries.Where(e => e.StudentId == s.Id).OrderByDescending(e => e.CreatedAt).Select(e => new { e.CreatedAt, e.Body }).FirstOrDefault(),
        }).ToListAsync(ct);
        return [.. rows.Select(r => new BookSummary(r.Id, r.Name, r.AdmissionNumber, r.Room, r.Last?.CreatedAt,
            r.Last is null ? null : r.Last.Body.Length > 120 ? r.Last.Body[..120] + "…" : r.Last.Body, unread.GetValueOrDefault(r.Id)))];
    }
}

// ---------- Lists ----------

/// <summary>The books of a class (form teacher, admins). Admins may leave the class out to see every class's books with unread notes.</summary>
public sealed record ListClassBooksQuery(Guid? ClassRoomId) : IQuery<IReadOnlyList<BookSummary>>;

/// <summary>A parent's children's books, or a student's own.</summary>
public sealed record ListMyBooksQuery : IQuery<IReadOnlyList<BookSummary>>;

internal sealed class ListClassBooksHandler(IAppDbContext db, AccessRules access, ICurrentUser me) : IQueryHandler<ListClassBooksQuery, IReadOnlyList<BookSummary>>
{
    public async Task<Result<IReadOnlyList<BookSummary>>> Handle(ListClassBooksQuery q, CancellationToken ct)
    {
        var uid = me.UserId!.Value;
        var students = db.Students.AsNoTracking().Where(s => s.Status == StudentStatus.Active);
        if (q.ClassRoomId is { } room)
        {
            if (!await access.IsFormTeacherAsync(room, ct)) return Error.Forbidden("book.forbidden", "Only the class's form teacher can open its communication books.");
            students = students.Where(s => s.ClassRoomId == room);
        }
        else if (me.IsInRole(Roles.Admin))
            students = students.Where(s => db.BookEntries.Any(e => e.StudentId == s.Id && e.AuthorUserId != uid
                && !db.BookReads.Any(r => r.StudentId == s.Id && r.UserId == uid && r.ReadAt >= e.CreatedAt)));
        else
            students = students.Where(s => s.ClassRoom != null && s.ClassRoom.FormTeacher != null && s.ClassRoom.FormTeacher.UserId == uid);
        return Result<IReadOnlyList<BookSummary>>.Success(await db.SummariesAsync(students, uid, ct));
    }
}

internal sealed class ListMyBooksHandler(IAppDbContext db, AccessRules access, ICurrentUser me) : IQueryHandler<ListMyBooksQuery, IReadOnlyList<BookSummary>>
{
    public async Task<Result<IReadOnlyList<BookSummary>>> Handle(ListMyBooksQuery q, CancellationToken ct) =>
        Result<IReadOnlyList<BookSummary>>.Success(await db.SummariesAsync(access.PortalStudents().AsNoTracking(), me.UserId!.Value, ct));
}

// ---------- One book ----------

/// <summary><c>Before</c>: older notes (scrolling back). <c>After</c>: only notes written or corrected since then (polling while open).</summary>
public sealed record GetBookQuery(Guid StudentId, DateTimeOffset? Before = null, DateTimeOffset? After = null) : IQuery<BookPage>;
public sealed record WriteEntryCommand(Guid StudentId, string? Body, byte[]? File, string? FileName) : ICommand<BookEntryDto>;
public sealed record EditEntryCommand(Guid StudentId, Guid EntryId, string Body) : ICommand<BookEntryDto>;
public sealed record MarkBookReadCommand(Guid StudentId) : ICommand;
public sealed record GetBookFileQuery(Guid StudentId, Guid EntryId) : IQuery<HomeworkFileContent>;

internal sealed class GetBookHandler(IAppDbContext db, BookAccess books, ICurrentUser me, TimeProvider time) : IQueryHandler<GetBookQuery, BookPage>
{
    private const int PageSize = 50;

    public async Task<Result<BookPage>> Handle(GetBookQuery q, CancellationToken ct)
    {
        if (await books.OpenAsync(q.StudentId, ct) is not { } book) return BookErrors.NotFound;
        var uid = me.UserId!.Value;
        var now = time.GetUtcNow();
        var entries = db.BookEntries.AsNoTracking().Where(e => e.StudentId == q.StudentId);
        List<BookEntry> page;
        var hasOlder = false;
        if (q.After is { } after)
        {
            // Live: new notes and corrections since the client's cursor (a few seconds ago).
            var since = after.ToUniversalTime();
            page = await entries.Where(e => e.CreatedAt > since || e.EditedAt > since).OrderBy(e => e.CreatedAt).Take(200).ToListAsync(ct);
        }
        else
        {
            if (q.Before is { } before) entries = entries.Where(e => e.CreatedAt < before.ToUniversalTime());
            var newest = await entries.OrderByDescending(e => e.CreatedAt).Take(PageSize + 1).ToListAsync(ct);
            (page, hasOlder) = ([.. newest.Take(PageSize).OrderBy(e => e.CreatedAt)], newest.Count > PageSize);
        }
        var seenUpTo = await db.BookReads.AsNoTracking().Where(r => r.StudentId == q.StudentId && r.UserId != uid)
            .MaxAsync(r => (DateTimeOffset?)r.ReadAt, ct);
        var room = await db.ClassRooms.AsNoTracking().Where(r => r.Id == book.Student.ClassRoomId).Select(r => r.ClassLevel.Name + " " + r.Arm).FirstOrDefaultAsync(ct);
        return new BookPage(q.StudentId, $"{book.Student.FirstName} {book.Student.LastName}", room,
            [.. page.Select(e => e.Dto(uid, now))], hasOlder, seenUpTo, now.AddSeconds(-2)); // overlaps a little: a note saved mid-poll isn't missed (clients skip ids they have)
    }
}

internal sealed class WriteEntryHandler(IAppDbContext db, BookAccess books, ICurrentUser me, TimeProvider time, HomeworkFiles files, BookNotices notices)
    : ICommandHandler<WriteEntryCommand, BookEntryDto>
{
    public async Task<Result<BookEntryDto>> Handle(WriteEntryCommand c, CancellationToken ct)
    {
        if (await books.OpenAsync(c.StudentId, ct) is not { } book) return BookErrors.NotFound;
        if (BookEntry.CheckBody(c.Body, c.File is not null) is { } invalid) return invalid.ToError();
        Domain.Homework.HomeworkFile? attachment = null;
        if (c.File is not null && (attachment = await files.SaveAsync(c.File, c.FileName, "books", ct)) is null) return HomeworkFiles.Invalid;

        var now = time.GetUtcNow();
        var entry = new BookEntry
        {
            StudentId = c.StudentId, AuthorUserId = me.UserId!.Value, AuthorName = await books.MyNameAsync(ct), Side = book.Side,
            Body = c.Body?.Trim() ?? "", Attachment = attachment,
        };
        db.BookEntries.Add(entry);
        await notices.WrittenAsync(book.Student, entry, ct);
        await db.SaveChangesAsync(ct);
        // Writing a note means you've read the book up to and including it.
        await db.MarkReadAsync(c.StudentId, entry.AuthorUserId, entry.CreatedAt, ct);
        return entry.Dto(entry.AuthorUserId, now);
    }
}

internal sealed class EditEntryHandler(IAppDbContext db, BookAccess books, ICurrentUser me, TimeProvider time) : ICommandHandler<EditEntryCommand, BookEntryDto>
{
    public async Task<Result<BookEntryDto>> Handle(EditEntryCommand c, CancellationToken ct)
    {
        if (await books.OpenAsync(c.StudentId, ct) is null) return BookErrors.NotFound;
        if (await db.BookEntries.FirstOrDefaultAsync(e => e.Id == c.EntryId && e.StudentId == c.StudentId, ct) is not { } entry) return BookErrors.EntryNotFound;
        var now = time.GetUtcNow();
        if (entry.AuthorUserId != me.UserId) return Error.Forbidden("book.not_author", "Only the person who wrote a note can correct it.");
        if (!entry.CanEdit(me.UserId!.Value, now)) return BookErrors.TooLate;
        if (BookEntry.CheckBody(c.Body, entry.Attachment is not null) is { } invalid) return invalid.ToError();
        (entry.Body, entry.EditedAt) = (c.Body.Trim(), now);
        await db.SaveChangesAsync(ct);
        return entry.Dto(me.UserId.Value, now);
    }
}

internal sealed class MarkBookReadHandler(IAppDbContext db, BookAccess books, ICurrentUser me, TimeProvider time) : ICommandHandler<MarkBookReadCommand>
{
    public async Task<Result> Handle(MarkBookReadCommand c, CancellationToken ct)
    {
        if (await books.OpenAsync(c.StudentId, ct) is null) return BookErrors.NotFound;
        await db.MarkReadAsync(c.StudentId, me.UserId!.Value, time.GetUtcNow(), ct);
        return Result.Success();
    }
}

internal sealed class GetBookFileHandler(IAppDbContext db, BookAccess books, HomeworkFiles files) : IQueryHandler<GetBookFileQuery, HomeworkFileContent>
{
    public async Task<Result<HomeworkFileContent>> Handle(GetBookFileQuery q, CancellationToken ct)
    {
        if (await books.OpenAsync(q.StudentId, ct) is null) return BookErrors.NotFound;
        var entry = await db.BookEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == q.EntryId && e.StudentId == q.StudentId, ct);
        return entry?.Attachment is { } file && await files.OpenAsync(file, ct) is { } content ? content : BookErrors.EntryNotFound;
    }
}

/// <summary>Tells the other side about a new note: the family (guardians and the student) or the form teacher.</summary>
public sealed class BookNotices(IAppDbContext db, Notifier notifier)
{
    public async Task WrittenAsync(Student student, BookEntry entry, CancellationToken ct)
    {
        var preview = entry.Body.Length == 0 ? "Sent a file." : entry.Body.Length > 140 ? entry.Body[..140] + "…" : entry.Body;
        var key = $"book:{entry.Id}";
        var family = $"/portal/book/{student.Id}";
        if (entry.Side == BookSide.School)
        {
            await notifier.GuardiansAsync(student.Id, NotificationKind.BookEntry, $"New note about {student.FirstName} from {entry.AuthorName}", preview, family, key, ct);
            if (student.UserId is { } uid) notifier.User(uid, student.Id, NotificationKind.BookEntry, $"New note from {entry.AuthorName}", preview, family, key);
            return;
        }
        // From the family: the form teacher, and the other guardians (both parents see the conversation).
        var formTeacher = await db.ClassRooms.AsNoTracking().Where(r => r.Id == student.ClassRoomId && r.FormTeacher != null && r.FormTeacher.UserId != null)
            .Select(r => r.FormTeacher!.UserId).FirstOrDefaultAsync(ct);
        if (formTeacher is { } teacher && teacher != entry.AuthorUserId)
            notifier.User(teacher, student.Id, NotificationKind.BookEntry, $"{entry.AuthorName} wrote in {student.FirstName}'s book", preview, $"/books/{student.Id}", key);
        await notifier.GuardiansAsync(student.Id, NotificationKind.BookEntry, $"{entry.AuthorName} wrote in {student.FirstName}'s book", preview, family, key, ct, except: entry.AuthorUserId);
        if (student.UserId is { } sid && sid != entry.AuthorUserId)
            notifier.User(sid, student.Id, NotificationKind.BookEntry, $"{entry.AuthorName} wrote in your book", preview, family, key);
    }
}
