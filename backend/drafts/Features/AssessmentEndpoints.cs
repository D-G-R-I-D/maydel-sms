using System.ComponentModel.DataAnnotations;
using Maydel.Api.Auth;
using Maydel.Api.Common;
using Maydel.Domain.Assessment;
using Maydel.Domain.People;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Api.Features;

public sealed record ComponentDto(Guid Id, string Name, decimal MaxScore, int Order, bool IsActive);
public sealed record BandDto(Guid Id, string Grade, decimal MinScore, decimal MaxScore, string Remark);
public sealed record AssessmentSettings(IReadOnlyList<ComponentDto> Components, IReadOnlyList<BandDto> Bands);
public sealed record SaveSettingsRequest(IReadOnlyList<ComponentInput> Components, IReadOnlyList<BandInput> Bands);
public sealed record ComponentInput(Guid? Id, [property: Required, MaxLength(40)] string Name, [property: Range(1, 100)] decimal MaxScore, bool IsActive);
public sealed record BandInput([property: Required, MaxLength(4)] string Grade, [property: Range(0, 100)] decimal MinScore, [property: Range(0, 100)] decimal MaxScore, [property: Required, MaxLength(40)] string Remark);

public sealed record GradeSheetRow(Guid StudentId, string AdmissionNumber, string StudentName, IReadOnlyDictionary<Guid, decimal> Scores, decimal? Total, string? Grade, string? Comment, bool Locked);
public sealed record GradeSheet(Guid ClassRoomId, Guid SubjectId, Guid TermId, IReadOnlyList<ComponentHeader> Components, IReadOnlyList<GradeSheetRow> Rows);
public sealed record SaveGradeSheetRequest(Guid ClassRoomId, Guid SubjectId, Guid TermId, IReadOnlyList<GradeSheetEntry> Entries);
public sealed record GradeSheetEntry(Guid StudentId, IReadOnlyDictionary<Guid, decimal?> Scores, [property: MaxLength(1000)] string? Comment);

public sealed record ReportSummary(Guid StudentId, string AdmissionNumber, string StudentName, int SubjectsScored, decimal? Average, ReportStatus Status, string? FormTeacherComment, string? PrincipalComment);
public sealed record ReportCommentsRequest([property: MaxLength(2000)] string? FormTeacherComment, [property: MaxLength(2000)] string? PrincipalComment);
public sealed record PublishRequest(Guid ClassRoomId, Guid TermId);

public static class AssessmentEndpoints
{
    public static void MapAssessmentEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/assessment").WithTags("Assessment").RequireAuthorization(Policies.Academic);

        // ----- Settings: assessment components & grading scale -----
        g.MapGet("/settings", async (AppDbContext db, CancellationToken ct) => new AssessmentSettings(
            await db.AssessmentComponents.AsNoTracking().OrderBy(c => c.Order).Select(c => new ComponentDto(c.Id, c.Name, c.MaxScore, c.Order, c.IsActive)).ToListAsync(ct),
            await db.GradeBands.AsNoTracking().OrderByDescending(b => b.MinScore).Select(b => new BandDto(b.Id, b.Grade, b.MinScore, b.MaxScore, b.Remark)).ToListAsync(ct)));

        g.MapPut("/settings", async (SaveSettingsRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var activeTotal = req.Components.Where(c => c.IsActive).Sum(c => c.MaxScore);
            if (activeTotal != 100) return Problems.Validation("components", $"Active components must total 100 (currently {activeTotal}).");
            if (req.Bands.Count == 0 || req.Bands.Min(b => b.MinScore) != 0) return Problems.Validation("bands", "The grading scale must start at 0.");
            if (req.Bands.Select(b => b.MinScore).Distinct().Count() != req.Bands.Count) return Problems.Validation("bands", "Each band needs a different minimum score.");

            var existing = await db.AssessmentComponents.ToListAsync(ct);
            var order = 0;
            foreach (var input in req.Components)
            {
                var c = input.Id is { } id ? existing.FirstOrDefault(e => e.Id == id) : null;
                if (c is null) db.AssessmentComponents.Add(c = new AssessmentComponent { Name = input.Name });
                (c.Name, c.MaxScore, c.IsActive, c.Order) = (input.Name.Trim(), input.MaxScore, input.IsActive, ++order);
            }
            // Components with recorded scores are deactivated, never deleted, so history stays intact.
            foreach (var c in existing.Where(e => !req.Components.Any(i => i.Id == e.Id))) c.IsActive = false;

            db.GradeBands.RemoveRange(db.GradeBands);
            db.GradeBands.AddRange(req.Bands.Select(b => new GradeBand { Grade = b.Grade.Trim(), MinScore = b.MinScore, MaxScore = b.MaxScore, Remark = b.Remark.Trim() }));
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.Admin);

        // ----- Grade sheet: one subject, one class, one term -----
        g.MapGet("/gradesheet", async (Guid classRoomId, Guid subjectId, Guid termId, AppDbContext db, CurrentUser me, CancellationToken ct) =>
        {
            if (!await Access.CanGradeAsync(db, me, classRoomId, subjectId, ct)) return Problems.Forbidden("You are not assigned to this subject and class.");

            var components = await ActiveComponentsAsync(db, ct);
            var students = await db.Students.AsNoTracking()
                .Where(s => s.ClassRoomId == classRoomId && s.Status == StudentStatus.Active)
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .Select(s => new { s.Id, s.AdmissionNumber, Name = s.LastName + ", " + s.FirstName }).ToListAsync(ct);
            var scores = await db.SubjectScores.AsNoTracking().Include(s => s.Components)
                .Where(s => s.ClassRoomId == classRoomId && s.SubjectId == subjectId && s.TermId == termId)
                .ToDictionaryAsync(s => s.StudentId, ct);
            var locked = await PublishedStudentsAsync(db, classRoomId, termId, ct);

            return Results.Ok(new GradeSheet(classRoomId, subjectId, termId, components, students.Select(s =>
            {
                scores.TryGetValue(s.Id, out var sc);
                return new GradeSheetRow(s.Id, s.AdmissionNumber, s.Name,
                    sc?.Components.ToDictionary(c => c.ComponentId, c => c.Score) ?? [],
                    sc?.Total, sc?.Grade, sc?.TeacherComment, locked.Contains(s.Id));
            }).ToList()));
        });

        g.MapPut("/gradesheet", async (SaveGradeSheetRequest req, AppDbContext db, CurrentUser me, CancellationToken ct) =>
        {
            if (!await Access.CanGradeAsync(db, me, req.ClassRoomId, req.SubjectId, ct)) return Problems.Forbidden("You are not assigned to this subject and class.");

            var components = (await ActiveComponentsAsync(db, ct)).ToDictionary(c => c.Id);
            var scale = new GradingScale(await db.GradeBands.AsNoTracking().ToListAsync(ct));
            var classStudents = (await db.Students.Where(s => s.ClassRoomId == req.ClassRoomId).Select(s => s.Id).ToListAsync(ct)).ToHashSet();
            var locked = await PublishedStudentsAsync(db, req.ClassRoomId, req.TermId, ct);

            // Validate everything before writing anything.
            var errors = new Dictionary<string, string[]>();
            for (var i = 0; i < req.Entries.Count; i++)
            {
                var e = req.Entries[i];
                if (!classStudents.Contains(e.StudentId)) errors[$"entries[{i}]"] = ["Student is not in this class."];
                else if (locked.Contains(e.StudentId)) errors[$"entries[{i}]"] = ["This student's report is published. Unpublish it before changing scores."];
                foreach (var (componentId, value) in e.Scores)
                {
                    if (!components.TryGetValue(componentId, out var c)) errors[$"entries[{i}].scores"] = ["Unknown assessment component."];
                    else if (value is < 0 || value > c.MaxScore) errors[$"entries[{i}].scores"] = [$"{c.Name} must be between 0 and {c.MaxScore}."];
                }
            }
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var existing = await db.SubjectScores.Include(s => s.Components)
                .Where(s => s.ClassRoomId == req.ClassRoomId && s.SubjectId == req.SubjectId && s.TermId == req.TermId)
                .ToDictionaryAsync(s => s.StudentId, ct);

            foreach (var e in req.Entries)
            {
                var entered = e.Scores.Where(kv => kv.Value is not null).ToList();
                existing.TryGetValue(e.StudentId, out var score);
                if (entered.Count == 0 && string.IsNullOrWhiteSpace(e.Comment))
                {
                    if (score is not null) db.SubjectScores.Remove(score);
                    continue;
                }
                if (score is null)
                {
                    score = new SubjectScore { StudentId = e.StudentId, ClassRoomId = req.ClassRoomId, SubjectId = req.SubjectId, TermId = req.TermId };
                    db.SubjectScores.Add(score);
                }
                score.Components.Clear();
                score.Components.AddRange(entered.Select(kv => new ComponentScore { ComponentId = kv.Key, Score = kv.Value!.Value }));
                score.TeacherComment = e.Comment?.Trim();
                score.EnteredByUserId = me.UserId;
                score.Recalculate(scale);
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ----- Report cards -----
        g.MapGet("/reports", async (Guid classRoomId, Guid termId, AppDbContext db, CancellationToken ct) =>
        {
            var students = await db.Students.AsNoTracking().Where(s => s.ClassRoomId == classRoomId && s.Status == StudentStatus.Active)
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .Select(s => new { s.Id, s.AdmissionNumber, Name = s.FirstName + " " + s.LastName }).ToListAsync(ct);
            var scores = await db.SubjectScores.AsNoTracking().Where(s => s.ClassRoomId == classRoomId && s.TermId == termId)
                .GroupBy(s => s.StudentId).Select(gr => new { gr.Key, Count = gr.Count(), Avg = gr.Average(x => x.Total) })
                .ToDictionaryAsync(x => x.Key, ct);
            var reports = await db.TermReports.AsNoTracking().Where(r => r.ClassRoomId == classRoomId && r.TermId == termId)
                .ToDictionaryAsync(r => r.StudentId, ct);

            return students.Select(s =>
            {
                scores.TryGetValue(s.Id, out var sc);
                reports.TryGetValue(s.Id, out var r);
                return new ReportSummary(s.Id, s.AdmissionNumber, s.Name, sc?.Count ?? 0, sc is null ? null : Math.Round(sc.Avg, 2),
                    r?.Status ?? ReportStatus.Draft, r?.FormTeacherComment, r?.PrincipalComment);
            });
        });

        g.MapGet("/reports/{studentId:guid}/{termId:guid}", async (Guid studentId, Guid termId, ReportCalculator calc, CancellationToken ct) =>
            await calc.BuildAsync(studentId, termId, publishedOnly: false, ct) is { } card ? Results.Ok(card) : Problems.NotFound());

        g.MapPut("/reports/{studentId:guid}/{termId:guid}/comments", async (Guid studentId, Guid termId, ReportCommentsRequest req, AppDbContext db, CurrentUser me, CancellationToken ct) =>
        {
            var student = await db.Students.FindAsync([studentId], ct);
            if (student?.ClassRoomId is not { } classRoomId) return Problems.NotFound();
            var isAdmin = me.IsInRole(Domain.Common.Roles.Admin);
            if (!isAdmin && !await Access.IsFormTeacherAsync(db, me, classRoomId, ct)) return Problems.Forbidden("Only the form teacher can comment on this report.");

            var report = await db.TermReports.FirstOrDefaultAsync(r => r.StudentId == studentId && r.TermId == termId, ct);
            if (report is null) db.TermReports.Add(report = new TermReport { StudentId = studentId, TermId = termId, ClassRoomId = classRoomId });
            if (report.Status == ReportStatus.Published) return Problems.Rule("Unpublish the report before editing comments.");

            report.FormTeacherComment = req.FormTeacherComment?.Trim();
            if (isAdmin) report.PrincipalComment = req.PrincipalComment?.Trim();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/reports/publish", async (PublishRequest req, ReportCalculator calc, CurrentUser me, CancellationToken ct) =>
            Results.Ok(new { Published = await calc.PublishAsync(req.ClassRoomId, req.TermId, me.RequiredUserId, ct) }))
            .RequireAuthorization(Policies.Admin);

        g.MapPost("/reports/unpublish", async (PublishRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var reports = await db.TermReports.Where(r => r.ClassRoomId == req.ClassRoomId && r.TermId == req.TermId && r.Status == ReportStatus.Published).ToListAsync(ct);
            reports.ForEach(r => { r.Status = ReportStatus.Draft; r.PublishedAt = null; });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { Unpublished = reports.Count });
        }).RequireAuthorization(Policies.Admin);
    }

    private static Task<List<ComponentHeader>> ActiveComponentsAsync(AppDbContext db, CancellationToken ct) =>
        db.AssessmentComponents.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Order)
            .Select(c => new ComponentHeader(c.Id, c.Name, c.MaxScore)).ToListAsync(ct);

    private static async Task<HashSet<Guid>> PublishedStudentsAsync(AppDbContext db, Guid classRoomId, Guid termId, CancellationToken ct) =>
        (await db.TermReports.Where(r => r.ClassRoomId == classRoomId && r.TermId == termId && r.Status == ReportStatus.Published)
            .Select(r => r.StudentId).ToListAsync(ct)).ToHashSet();
}
