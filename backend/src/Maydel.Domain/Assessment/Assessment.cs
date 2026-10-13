using Maydel.Domain.Academics;
using Maydel.Domain.Common;
using Maydel.Domain.People;

namespace Maydel.Domain.Assessment;

/// <summary>
/// One column of continuous assessment (e.g. "CAT / 30", "Exams / 40").
/// Configurable by the school instead of hardcoded; active components must total 100.
/// </summary>
public class AssessmentComponent : Entity
{
    public required string Name { get; set; }
    public decimal MaxScore { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A grade band, e.g. 70–100 => "A", "Excellent".</summary>
public class GradeBand : Entity
{
    public required string Grade { get; set; }
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public required string Remark { get; set; }
}

/// <summary>A student's scores for one subject in one term.</summary>
public class SubjectScore : Entity
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid TermId { get; set; }
    public Term Term { get; set; } = null!;
    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public Guid ClassRoomId { get; set; }

    public List<ComponentScore> Components { get; set; } = [];

    /// <summary>Denormalised for ranking; recomputed on every save.</summary>
    public decimal Total { get; set; }
    public string? Grade { get; set; }
    public string? Remark { get; set; }
    public string? TeacherComment { get; set; }
    public Guid? EnteredByUserId { get; set; }

    public void Recalculate(GradingScale scale)
    {
        Total = Components.Sum(c => c.Score);
        var band = scale.BandFor(Total);
        Grade = band?.Grade;
        Remark = band?.Remark;
    }

    /// <summary>Replaces the component scores and recomputes total and grade. Callers validate with <see cref="AssessmentRules.CheckScore"/> first.</summary>
    public void SetScores(IEnumerable<(Guid ComponentId, decimal Score)> scores, GradingScale scale)
    {
        Components.Clear();
        Components.AddRange(scores.Select(s => new ComponentScore { ComponentId = s.ComponentId, Score = s.Score }));
        Recalculate(scale);
    }
}

public class ComponentScore
{
    public Guid SubjectScoreId { get; set; }
    public Guid ComponentId { get; set; }
    public AssessmentComponent Component { get; set; } = null!;
    public decimal Score { get; set; }
}

public enum ReportStatus
{
    Draft,
    Published,
}

/// <summary>
/// The term report card for one student. Parents only see it once it is Published,
/// fixing the old system where half-entered results were immediately visible.
/// </summary>
public class TermReport : Entity
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid TermId { get; set; }
    public Term Term { get; set; } = null!;
    public Guid ClassRoomId { get; set; }
    public ClassRoom ClassRoom { get; set; } = null!;

    public int DaysOpened { get; set; }
    public int DaysPresent { get; set; }
    public string? FormTeacherComment { get; set; }
    public string? PrincipalComment { get; set; }

    // Computed at publish time so the printed report never changes underneath parents.
    public decimal Average { get; set; }
    public decimal ClassAverage { get; set; }
    public int? Position { get; set; }
    public int ClassSize { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }

    public int DaysAbsent => Math.Max(0, DaysOpened - DaysPresent);
}

/// <summary>Pure grading logic, kept free of persistence so it can be unit tested.</summary>
public sealed class GradingScale
{
    private readonly IReadOnlyList<GradeBand> _bands;

    public GradingScale(IEnumerable<GradeBand> bands)
    {
        _bands = bands.OrderByDescending(b => b.MinScore).ToList();
    }

    /// <summary>Highest band whose minimum the score reaches, so fractional totals (69.5) never fall into a gap.</summary>
    public GradeBand? BandFor(decimal score) => _bands.FirstOrDefault(b => score >= b.MinScore);

    /// <summary>Competition ranking ("1, 2, 2, 4"): ties share a position.</summary>
    public static Dictionary<Guid, int> Rank(IReadOnlyDictionary<Guid, decimal> averages)
    {
        var ordered = averages.OrderByDescending(a => a.Value).ToList();
        var result = new Dictionary<Guid, int>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var position = i > 0 && ordered[i].Value == ordered[i - 1].Value
                ? result[ordered[i - 1].Key]
                : i + 1;
            result[ordered[i].Key] = position;
        }
        return result;
    }
}

/// <summary>Assessment configuration and score rules (E2.4, E5.1). Pure, unit-tested.</summary>
public static class AssessmentRules
{
    public const decimal FullMarks = 100;

    public sealed record ComponentInput(string Name, decimal MaxScore, bool IsActive);

    public sealed record BandInput(string Grade, decimal MinScore, decimal MaxScore, string Remark);

    /// <summary>At least one active component; names unique; each max in (0, 100] with at most 2 decimals; active total exactly 100.</summary>
    public static DomainError? CheckComponents(IReadOnlyList<ComponentInput> components)
    {
        if (!components.Any(c => c.IsActive)) return new("assessment.no_components", "Keep at least one active component.", "components");
        if (components.Any(c => string.IsNullOrWhiteSpace(c.Name)))
            return new("assessment.component_name", "Every component needs a name.", "components");
        if (components.GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            return new("assessment.component_duplicate", "Component names must be different.", "components");
        if (components.Any(c => c.MaxScore <= 0 || c.MaxScore > FullMarks || decimal.Round(c.MaxScore, 2) != c.MaxScore))
            return new("assessment.component_max", "Each component's maximum must be above 0 and at most 100.", "components");
        var total = components.Where(c => c.IsActive).Sum(c => c.MaxScore);
        if (total != FullMarks)
            return new("assessment.components_total", $"Active components must add up to 100 (they add up to {total:0.##}).", "components");
        return null;
    }

    /// <summary>
    /// Bands cover 0–100: lowest minimum is 0, minima are unique, grades unique, each band's max ≥ its min and ≤ 100.
    /// Lookup is by minimum only (<see cref="GradingScale.BandFor"/>), so a total like 69.5 never falls into a gap.
    /// </summary>
    public static DomainError? CheckBands(IReadOnlyList<BandInput> bands)
    {
        if (bands.Count == 0 || bands.Min(b => b.MinScore) != 0)
            return new("assessment.bands_start", "The grading scale must start at 0.", "bands");
        if (bands.Select(b => b.MinScore).Distinct().Count() != bands.Count)
            return new("assessment.bands_duplicate_min", "Each grade needs a different minimum score.", "bands");
        if (bands.GroupBy(b => b.Grade.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1 || string.IsNullOrWhiteSpace(g.Key)))
            return new("assessment.bands_grade", "Each band needs its own grade letter.", "bands");
        if (bands.Any(b => b.MinScore < 0 || b.MaxScore > FullMarks || b.MaxScore < b.MinScore))
            return new("assessment.bands_range", "Each band's range must sit within 0–100, with the maximum not below the minimum.", "bands");
        return null;
    }

    /// <summary>A score is 0..max with at most 2 decimal places.</summary>
    public static string? CheckScore(decimal score, AssessmentComponent component)
    {
        if (score < 0 || score > component.MaxScore) return $"{component.Name} must be between 0 and {component.MaxScore:0.##}.";
        if (decimal.Round(score, 2) != score) return $"{component.Name} can have at most 2 decimal places.";
        return null;
    }
}
