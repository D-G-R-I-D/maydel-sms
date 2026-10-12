using System.ComponentModel.DataAnnotations;
using Maydel.Api.Common;
using Maydel.Domain.Academics;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Api.Features;

public sealed record TermDto(Guid Id, TermNumber Number, string Name, DateOnly StartsOn, DateOnly EndsOn, DateOnly? MidTermStartsOn, DateOnly? MidTermEndsOn, bool IsCurrent);
public sealed record SessionDto(Guid Id, string Name, DateOnly StartsOn, DateOnly EndsOn, bool IsCurrent, IReadOnlyList<TermDto> Terms);
public sealed record UpsertSessionRequest([property: Required, RegularExpression(@"^\d{4}/\d{4}$")] string Name, DateOnly StartsOn, DateOnly EndsOn);
public sealed record UpsertTermRequest(TermNumber Number, DateOnly StartsOn, DateOnly EndsOn, DateOnly? MidTermStartsOn, DateOnly? MidTermEndsOn);

public sealed record ClassRoomDto(Guid Id, Guid ClassLevelId, string ClassLevel, SchoolSection Section, int LevelOrder, string Arm, string Name, Guid? FormTeacherId, string? FormTeacher, int Capacity, int StudentCount);
public sealed record UpsertClassRoomRequest(Guid ClassLevelId, [property: Required, MaxLength(32)] string Arm, Guid? FormTeacherId, [property: Range(1, 200)] int Capacity);

public sealed record SubjectDto(Guid Id, string Name, string Code, bool IsActive);
public sealed record UpsertSubjectRequest([property: Required, MaxLength(100)] string Name, [property: Required, MaxLength(10)] string Code, bool IsActive = true);

public sealed record ClassSubjectDto(Guid Id, Guid SubjectId, string Subject, string Code, Guid? TeacherId, string? Teacher);
public sealed record SetClassSubjectsRequest(IReadOnlyList<ClassSubjectAssignment> Subjects);
public sealed record ClassSubjectAssignment(Guid SubjectId, Guid? TeacherId);

public static class AcademicsEndpoints
{
    public static void MapAcademicsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/academics").WithTags("Academics").RequireAuthorization(Policies.Staff);
        var admin = g.MapGroup("").RequireAuthorization(Policies.Admin);

        // ----- Sessions & terms -----
        g.MapGet("/sessions", async (AppDbContext db, CancellationToken ct) =>
        {
            var sessions = await db.Sessions.AsNoTracking().Include(s => s.Terms).OrderByDescending(s => s.StartsOn).ToListAsync(ct);
            return sessions.Select(ToDto);
        });

        g.MapGet("/terms/current", async (AppDbContext db, CancellationToken ct) =>
        {
            var term = await db.Terms.AsNoTracking().Include(t => t.Session).FirstOrDefaultAsync(t => t.IsCurrent, ct);
            return term is null ? Problems.NotFound("No current term is set.") : Results.Ok(ToDto(term));
        }).RequireAuthorization();

        admin.MapPost("/sessions", async (UpsertSessionRequest req, AppDbContext db, CancellationToken ct) =>
        {
            if (req.EndsOn <= req.StartsOn) return Problems.Validation("endsOn", "Session must end after it starts.");
            var session = new AcademicSession { Name = req.Name, StartsOn = req.StartsOn, EndsOn = req.EndsOn };
            db.Sessions.Add(session);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/academics/sessions/{session.Id}", ToDto(session));
        });

        admin.MapPost("/sessions/{sessionId:guid}/terms", async (Guid sessionId, UpsertTermRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var session = await db.Sessions.Include(s => s.Terms).FirstOrDefaultAsync(s => s.Id == sessionId, ct);
            if (session is null) return Problems.NotFound();
            if (req.EndsOn <= req.StartsOn) return Problems.Validation("endsOn", "Term must end after it starts.");
            if (session.Terms.Any(t => t.Number == req.Number)) return Problems.Conflict($"{req.Number} term already exists for {session.Name}.");

            var term = new Term { Number = req.Number, StartsOn = req.StartsOn, EndsOn = req.EndsOn, MidTermStartsOn = req.MidTermStartsOn, MidTermEndsOn = req.MidTermEndsOn };
            session.Terms.Add(term);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/academics/terms/{term.Id}", ToDto(term));
        });

        admin.MapPut("/terms/{id:guid}", async (Guid id, UpsertTermRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var term = await db.Terms.Include(t => t.Session).FirstOrDefaultAsync(t => t.Id == id, ct);
            if (term is null) return Problems.NotFound();
            if (req.EndsOn <= req.StartsOn) return Problems.Validation("endsOn", "Term must end after it starts.");
            (term.StartsOn, term.EndsOn, term.MidTermStartsOn, term.MidTermEndsOn) = (req.StartsOn, req.EndsOn, req.MidTermStartsOn, req.MidTermEndsOn);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(term));
        });

        // Exactly one current term (and its session) at a time.
        admin.MapPost("/terms/{id:guid}/make-current", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var term = await db.Terms.Include(t => t.Session).FirstOrDefaultAsync(t => t.Id == id, ct);
            if (term is null) return Problems.NotFound();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Terms.Where(t => t.IsCurrent).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsCurrent, false), ct);
            await db.Sessions.Where(s => s.IsCurrent).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsCurrent, false), ct);
            term.IsCurrent = true;
            term.Session.IsCurrent = true;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        // ----- Class levels & rooms -----
        g.MapGet("/class-levels", async (AppDbContext db, CancellationToken ct) =>
            await db.ClassLevels.AsNoTracking().OrderBy(l => l.Order)
                .Select(l => new { l.Id, l.Name, l.Section, l.Order }).ToListAsync(ct));

        g.MapGet("/classrooms", async (AppDbContext db, CancellationToken ct) =>
            await QueryClassRooms(db).ToListAsync(ct));

        admin.MapPost("/classrooms", async (UpsertClassRoomRequest req, AppDbContext db, CancellationToken ct) =>
        {
            if (!await db.ClassLevels.AnyAsync(l => l.Id == req.ClassLevelId, ct)) return Problems.Validation("classLevelId", "Unknown class level.");
            var room = new ClassRoom { ClassLevelId = req.ClassLevelId, Arm = req.Arm.Trim(), FormTeacherId = req.FormTeacherId, Capacity = req.Capacity };
            db.ClassRooms.Add(room);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/academics/classrooms/{room.Id}", await QueryClassRooms(db).FirstAsync(r => r.Id == room.Id, ct));
        });

        admin.MapPut("/classrooms/{id:guid}", async (Guid id, UpsertClassRoomRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var room = await db.ClassRooms.FindAsync([id], ct);
            if (room is null) return Problems.NotFound();
            (room.Arm, room.FormTeacherId, room.Capacity) = (req.Arm.Trim(), req.FormTeacherId, req.Capacity);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await QueryClassRooms(db).FirstAsync(r => r.Id == id, ct));
        });

        g.MapGet("/classrooms/{id:guid}/subjects", async (Guid id, AppDbContext db, CancellationToken ct) =>
            await db.ClassSubjects.AsNoTracking().Where(cs => cs.ClassRoomId == id).OrderBy(cs => cs.Subject.Name)
                .Select(cs => new ClassSubjectDto(cs.Id, cs.SubjectId, cs.Subject.Name, cs.Subject.Code, cs.TeacherId, cs.Teacher != null ? cs.Teacher.FullName : null))
                .ToListAsync(ct));

        // Replaces the subject list for a class room (adds, removes and reassigns teachers in one call).
        admin.MapPut("/classrooms/{id:guid}/subjects", async (Guid id, SetClassSubjectsRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var existing = await db.ClassSubjects.Where(cs => cs.ClassRoomId == id).ToListAsync(ct);
            var wanted = req.Subjects.ToDictionary(s => s.SubjectId);
            db.ClassSubjects.RemoveRange(existing.Where(e => !wanted.ContainsKey(e.SubjectId)));
            foreach (var a in req.Subjects)
            {
                var row = existing.FirstOrDefault(e => e.SubjectId == a.SubjectId);
                if (row is null) db.ClassSubjects.Add(new ClassSubject { ClassRoomId = id, SubjectId = a.SubjectId, TeacherId = a.TeacherId });
                else row.TeacherId = a.TeacherId;
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ----- Subjects -----
        g.MapGet("/subjects", async (AppDbContext db, CancellationToken ct) =>
            await db.Subjects.AsNoTracking().OrderBy(s => s.Name).Select(s => new SubjectDto(s.Id, s.Name, s.Code, s.IsActive)).ToListAsync(ct));

        admin.MapPost("/subjects", async (UpsertSubjectRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var subject = new Subject { Name = req.Name.Trim(), Code = req.Code.Trim().ToUpperInvariant(), IsActive = req.IsActive };
            db.Subjects.Add(subject);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/academics/subjects/{subject.Id}", new SubjectDto(subject.Id, subject.Name, subject.Code, subject.IsActive));
        });

        admin.MapPut("/subjects/{id:guid}", async (Guid id, UpsertSubjectRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var subject = await db.Subjects.FindAsync([id], ct);
            if (subject is null) return Problems.NotFound();
            (subject.Name, subject.Code, subject.IsActive) = (req.Name.Trim(), req.Code.Trim().ToUpperInvariant(), req.IsActive);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new SubjectDto(subject.Id, subject.Name, subject.Code, subject.IsActive));
        });
    }

    internal static IQueryable<ClassRoomDto> QueryClassRooms(AppDbContext db) =>
        db.ClassRooms.AsNoTracking()
            .OrderBy(r => r.ClassLevel.Order).ThenBy(r => r.Arm)
            .Select(r => new ClassRoomDto(
                r.Id, r.ClassLevelId, r.ClassLevel.Name, r.ClassLevel.Section, r.ClassLevel.Order, r.Arm,
                r.ClassLevel.Name + " " + r.Arm, r.FormTeacherId, r.FormTeacher != null ? r.FormTeacher.FullName : null, r.Capacity,
                db.Students.Count(s => s.ClassRoomId == r.Id && s.Status == Domain.People.StudentStatus.Active)));

    internal static TermDto ToDto(Term t) =>
        new(t.Id, t.Number, $"{t.Number} Term {t.Session?.Name}".Trim(), t.StartsOn, t.EndsOn, t.MidTermStartsOn, t.MidTermEndsOn, t.IsCurrent);

    private static SessionDto ToDto(AcademicSession s) =>
        new(s.Id, s.Name, s.StartsOn, s.EndsOn, s.IsCurrent, s.Terms.OrderBy(t => t.Number).Select(ToDto).ToList());
}
