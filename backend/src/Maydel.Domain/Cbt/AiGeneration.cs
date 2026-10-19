using Maydel.Domain.Common;

namespace Maydel.Domain.Cbt;

public enum AssessmentType
{
    Test,
    Cat,
    Exam,
}

public enum GenerationStatus
{
    Ready,
    /// <summary>The AI declined (e.g. a safety filter) or returned nothing usable; the allowance isn't charged.</summary>
    Failed,
}

/// <summary>
/// One AI question-writing request by a teacher (E16.8): the scope, what to write, the uploaded material, and each version
/// generated (the first plus up to <see cref="MaxRegenerations"/> regenerations). Only the teacher's own material and the
/// subject/class names are sent to the AI, never student data.
/// </summary>
public class AiGeneration : Entity
{
    public const int MaxRegenerations = 5;
    public const int MaxQuestions = 40;
    public const int MaxFiles = 5;

    public Guid UserId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid ClassLevelId { get; set; }
    public Guid? TermId { get; set; }
    public AssessmentType AssessmentType { get; set; }
    public List<QuestionKind> Kinds { get; set; } = [];
    public int Count { get; set; }
    public int Easy { get; set; }
    public int Medium { get; set; }
    public int Hard { get; set; }

    /// <summary>Extra guidance from the teacher (topics to cover, curriculum notes).</summary>
    public string? Guidance { get; set; }
    public List<AiSourceFile> Files { get; set; } = [];
    public List<AiVersion> Versions { get; set; } = [];

    public int Regenerations => Math.Max(0, Versions.Count(v => v.Status == GenerationStatus.Ready) - 1);
    public bool CanRegenerate => Regenerations < MaxRegenerations;

    /// <summary>Checks the request shape: kinds, a count of 1–40 split exactly across the difficulty mix, and the files.</summary>
    public DomainError? Check()
    {
        if (Kinds.Count == 0 || Kinds.Any(k => !Enum.IsDefined(k))) return new("ai.kinds", "Choose at least one kind of question.", "kinds");
        Kinds = [.. Kinds.Distinct()];
        if (Count is < 1 or > MaxQuestions) return new("ai.count", $"Ask for between 1 and {MaxQuestions} questions.", "count");
        if (Easy < 0 || Medium < 0 || Hard < 0 || Easy + Medium + Hard != Count)
            return new("ai.mix", "The easy, medium and hard numbers must add up to the number of questions.", "easy");
        if (Files.Count is 0 && string.IsNullOrWhiteSpace(Guidance))
            return new("ai.material", "Upload your scheme of work or notes (PDF or photos), or describe the topics to cover.", "files");
        if (Files.Count > MaxFiles) return new("ai.files", $"Upload at most {MaxFiles} files.", "files");
        if (Guidance is { Length: > 4000 }) return new("ai.guidance", "Keep the guidance under 4000 characters.", "guidance");
        return null;
    }
}

public class AiSourceFile
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public required string FilePath { get; set; }
    public required string ContentType { get; set; }
}

public class AiVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public int Number { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public GenerationStatus Status { get; set; }
    public string? Message { get; set; }

    /// <summary>The generated questions as JSON (a list of <c>GeneratedQuestion</c>); kept so any version can be accepted later.</summary>
    public string QuestionsJson { get; set; } = "[]";
    public string? Model { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}

/// <summary>The school's AI settings (E16.9): on/off, the default monthly allowance per teacher, and a school-wide monthly cap.</summary>
public class AiSettings : Entity
{
    public bool Enabled { get; set; } = true;
    public int DefaultMonthlyGenerations { get; set; } = 30;
    public int SchoolMonthlyGenerations { get; set; } = 1000;

    public static DomainError? Check(int teacherDefault, int school)
    {
        if (teacherDefault is < 0 or > 1000) return new("ai.allowance", "The allowance per teacher must be between 0 and 1000 a month.", "defaultMonthlyGenerations");
        if (school is < 0 or > 100000) return new("ai.cap", "The school cap must be between 0 and 100000 a month.", "schoolMonthlyGenerations");
        return null;
    }
}

/// <summary>A teacher's own monthly allowance, overriding the default (E16.9).</summary>
public class AiAllowance : Entity
{
    public Guid UserId { get; set; }
    public int MonthlyGenerations { get; set; }
}

/// <summary>Quota arithmetic (E16.9): each generation or regeneration that succeeds uses one from the teacher's month and the school's.</summary>
public static class AiQuota
{
    /// <summary>The first instant of the calendar month in Abuja, and of the next one, as UTC.</summary>
    public static (DateTimeOffset From, DateTimeOffset To) Month(DateTimeOffset now)
    {
        var local = now.ToOffset(Attendance.RegisterRules.SchoolUtcOffset);
        var from = new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, Attendance.RegisterRules.SchoolUtcOffset);
        return (from.ToUniversalTime(), from.AddMonths(1).ToUniversalTime());
    }

    public static DomainError? CanSpend(int allowance, int used, int schoolCap, int schoolUsed, bool enabled)
    {
        if (!enabled) return new("ai.disabled", "AI question writing is switched off for the school.");
        if (used >= allowance) return new("ai.quota", $"You've used all {allowance} AI generations for this month. Your allowance resets on the 1st.");
        if (schoolUsed >= schoolCap) return new("ai.school_quota", "The school has used this month's AI allowance. Please ask the admin.");
        return null;
    }
}
