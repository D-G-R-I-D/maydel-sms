using Maydel.Application.Abstractions;
using Maydel.Domain.Assessment;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Assessment;

/// <summary><c>HasScores</c>: scores exist for it, so it can be deactivated but not removed, and its maximum can't drop below them.</summary>
public sealed record ComponentDto(Guid Id, string Name, decimal MaxScore, int Order, bool IsActive, bool HasScores);

public sealed record GradeBandDto(string Grade, decimal MinScore, decimal MaxScore, string Remark);

/// <summary>A ready-made grading scale. Apply it by sending its bands in <c>PUT /api/assessment/settings</c>.</summary>
public sealed record GradingPresetDto(string Key, string Name, string Description, IReadOnlyList<GradeBandDto> Bands);

public sealed record AssessmentSettings(IReadOnlyList<ComponentDto> Components, IReadOnlyList<GradeBandDto> Bands);

/// <summary>Component in a save request. <c>Id</c> null = new component; existing components missing from the list are removed or deactivated.</summary>
public sealed record ComponentInput(Guid? Id, string Name, decimal MaxScore, bool IsActive);

/// <summary>A class + subject the caller may enter scores for. <c>IsFormClass</c>: they are the form teacher of that class.</summary>
public sealed record GradableClass(Guid ClassRoomId, string ClassRoom, int LevelOrder, Guid SubjectId, string Subject, bool IsFormClass);

public sealed record GradeSheetComponent(Guid Id, string Name, decimal MaxScore);

public sealed record ComponentScoreValue(Guid ComponentId, decimal? Score);

/// <summary>One student's line. <c>Locked</c>: their report for this term is published, so scores are read-only (E5.2).</summary>
public sealed record GradeSheetRow(
    Guid StudentId, string AdmissionNumber, string Name, IReadOnlyList<ComponentScoreValue> Scores,
    decimal? Total, string? Grade, string? Remark, string? Comment, bool Locked);

public sealed record GradeSheet(
    Guid ClassRoomId, string ClassRoom, Guid SubjectId, string Subject, Guid TermId, string Term,
    IReadOnlyList<GradeSheetComponent> Components, IReadOnlyList<GradeSheetRow> Rows);

/// <summary>A student's scores to save. Null score = not entered. A row with no scores and no comment clears that student's entry.</summary>
public sealed record GradeSheetEntry(Guid StudentId, IReadOnlyList<ComponentScoreValue> Scores, string? Comment);

public static class AssessmentErrors
{
    public static readonly Common.Error NotGrader = Common.Error.Forbidden("assessment.not_assigned", "You're not assigned to teach this subject in this class.");
    public static readonly Common.Error NotOffered = Common.Error.NotFound("assessment.subject_not_offered", "This class doesn't offer that subject.");
    public static readonly Common.Error TermNotFound = Common.Error.NotFound("term.not_found", "That term doesn't exist.");
    public static Common.Error Published(IEnumerable<string> names) => Common.Error.Rule("assessment.report_published",
        $"Report cards are already published for {string.Join(", ", names)}. An administrator must unpublish them before scores can change.");
}

public static class AssessmentQueries
{
    public static async Task<GradingScale> GradingScaleAsync(this IAppDbContext db, CancellationToken ct) =>
        new(await db.GradeBands.AsNoTracking().ToListAsync(ct));

    public static Task<List<AssessmentComponent>> ActiveComponentsAsync(this IAppDbContext db, CancellationToken ct) =>
        db.AssessmentComponents.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Order).ToListAsync(ct);

    /// <summary>Students in this class whose report for the term is published.</summary>
    public static async Task<HashSet<Guid>> PublishedStudentsAsync(this IAppDbContext db, Guid classRoomId, Guid termId, CancellationToken ct) =>
        [.. await db.TermReports.Where(r => r.ClassRoomId == classRoomId && r.TermId == termId && r.Status == ReportStatus.Published)
            .Select(r => r.StudentId).ToListAsync(ct)];

    public static async Task<AssessmentSettings> GetSettingsAsync(this IAppDbContext db, CancellationToken ct)
    {
        var scored = await db.SubjectScores.SelectMany(s => s.Components).Select(c => c.ComponentId).Distinct().ToListAsync(ct);
        var components = await db.AssessmentComponents.AsNoTracking().OrderBy(c => c.Order).ToListAsync(ct);
        var bands = await db.GradeBands.AsNoTracking().OrderByDescending(b => b.MinScore)
            .Select(b => new GradeBandDto(b.Grade, b.MinScore, b.MaxScore, b.Remark)).ToListAsync(ct);
        return new AssessmentSettings(
            [.. components.Select(c => new ComponentDto(c.Id, c.Name, c.MaxScore, c.Order, c.IsActive, scored.Contains(c.Id)))], bands);
    }
}
