using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Attendance;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Maydel.Application.Communication;

/// <summary>
/// The evening email (E17.2): one per person, listing the communication-book notes they haven't read yet, by child. Sent from
/// 18:00 WAT, at most once a day, only to people with an email address and something new since their last summary.
/// Portal notifications still arrive as each note is written; this is for those who don't check the portal.
/// </summary>
public sealed record SendBookDigestCommand(DateTimeOffset? Now = null) : ICommand<int>;

internal sealed class SendBookDigestHandler(IAppDbContext db, ISchoolClock clock, IOptions<EmailOptions> options) : ICommandHandler<SendBookDigestCommand, int>
{
    public const int Hour = 18;

    public async Task<Result<int>> Handle(SendBookDigestCommand c, CancellationToken ct)
    {
        var now = (c.Now ?? clock.Now).ToUniversalTime();
        if (now.ToOffset(RegisterRules.SchoolUtcOffset).Hour < Hour) return 0;
        var since = now.AddDays(-1);
        var entries = await db.BookEntries.AsNoTracking().Where(e => e.CreatedAt > since).OrderBy(e => e.CreatedAt).ToListAsync(ct);
        if (entries.Count == 0) return 0;

        // Who should hear about each note: the other side of the book (as in BookNotices).
        var studentIds = entries.Select(e => e.StudentId).Distinct().ToList();
        var students = await db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id, s.FirstName, s.UserId,
                FormTeacher = s.ClassRoom != null && s.ClassRoom.FormTeacher != null ? s.ClassRoom.FormTeacher.UserId : null,
                Guardians = s.Guardians.Where(g => g.Guardian.UserId != null).Select(g => g.Guardian.UserId!.Value).ToList(),
            }).ToDictionaryAsync(s => s.Id, ct);
        var reads = (await db.BookReads.AsNoTracking().Where(r => studentIds.Contains(r.StudentId)).ToListAsync(ct))
            .ToDictionary(r => (r.StudentId, r.UserId), r => r.ReadAt);

        var unread = new Dictionary<Guid, List<BookEntry>>();
        foreach (var e in entries)
        {
            if (!students.TryGetValue(e.StudentId, out var s)) continue;
            var audience = e.Side == BookSide.School
                ? s.Guardians.Append(s.UserId ?? Guid.Empty)
                : s.Guardians.Append(s.FormTeacher ?? Guid.Empty).Append(s.UserId ?? Guid.Empty);
            foreach (var user in audience.Where(u => u != Guid.Empty && u != e.AuthorUserId).Distinct())
            {
                if (reads.TryGetValue((e.StudentId, user), out var readAt) && readAt >= e.CreatedAt) continue;
                (unread.TryGetValue(user, out var list) ? list : unread[user] = []).Add(e);
            }
        }
        if (unread.Count == 0) return 0;

        var userIds = unread.Keys.ToList();
        var marks = await db.BookDigestMarks.Where(m => userIds.Contains(m.UserId)).ToDictionaryAsync(m => m.UserId, ct);
        var people = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id) && u.IsActive && u.Email != null && u.Email != "")
            .Select(u => new { u.Id, u.FullName, Email = u.Email! }).ToListAsync(ct);
        var school = await db.SchoolProfiles.AsNoTracking().OrderBy(p => p.CreatedAt).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? options.Value.FromName;
        var portal = (options.Value.PortalUrl ?? "").TrimEnd('/');
        var today = RegisterRules.TodayAt(now);

        var sent = 0;
        foreach (var person in people)
        {
            marks.TryGetValue(person.Id, out var mark);
            // Once a day, and only notes that arrived after the last summary.
            if (mark is not null && RegisterRules.TodayAt(mark.SentAt) == today) continue;
            var fresh = unread[person.Id].Where(e => mark is null || e.CreatedAt > mark.SentAt).ToList();
            if (fresh.Count == 0) continue;

            var lines = new List<string> { $"Dear {person.FullName},", "", $"There {(fresh.Count == 1 ? "is 1 new note" : $"are {fresh.Count} new notes")} in the communication book that you haven't read yet:", "" };
            foreach (var group in fresh.GroupBy(e => e.StudentId))
            {
                var child = students[group.Key].FirstName;
                lines.Add($"{child}: {group.Count()} new {(group.Count() == 1 ? "note" : "notes")}");
                foreach (var e in group.TakeLast(3))
                {
                    var text = e.Body.Length == 0 ? "(a file)" : e.Body.Length > 140 ? e.Body[..140] + "…" : e.Body;
                    lines.Add($"  • {e.AuthorName}: {text.ReplaceLineEndings(" ")}");
                }
                lines.Add("");
            }
            if (portal.Length > 0) lines.AddRange([$"Read and reply in the portal: {portal}", ""]);
            lines.Add(school);

            db.OutboxEmails.Add(new OutboxEmail
            {
                Kind = EmailKind.BookDigest, ToAddress = person.Email.Trim(), ToName = person.FullName,
                Subject = $"New in the communication book – {school}", Body = string.Join("\n", lines),
                NextAttemptAt = clock.Now.ToUniversalTime(), // queued now, even when run as of another time
            });
            if (mark is null) db.BookDigestMarks.Add(new BookDigestMark { UserId = person.Id, SentAt = now });
            else mark.SentAt = now;
            sent++;
        }
        await db.SaveChangesAsync(ct);
        return sent;
    }
}
