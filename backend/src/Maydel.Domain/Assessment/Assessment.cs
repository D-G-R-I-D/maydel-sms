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
