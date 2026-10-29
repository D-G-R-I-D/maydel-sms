using Maydel.Domain.Common;
using Maydel.Domain.People;

namespace Maydel.Domain.Academics;

/// <summary>What happens to a student at the end of a session (E3.6).</summary>
public enum PromotionOutcome
{
    /// <summary>Moves up to a class in the next level.</summary>
    Promote,

    /// <summary>Stays in the same class for another year.</summary>
    Repeat,

    /// <summary>Leaves the school having finished (the top class, or by the school's decision).</summary>
    Graduate,

    /// <summary>Leaves the school.</summary>
    Withdraw,
}

/// <summary>
/// One student's end-of-session decision, kept as history. A student has at most one per session, so a class can't be promoted
/// twice by mistake; undoing a run deletes its records.
/// </summary>
public class PromotionRecord : Entity
{
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public PromotionOutcome Outcome { get; set; }
    public Guid FromClassRoomId { get; set; }
    public ClassRoom FromClassRoom { get; set; } = null!;

    /// <summary>The class the student is in afterwards (the same class for Repeat; their last class for Graduate and Withdraw).</summary>
    public Guid ToClassRoomId { get; set; }
    public ClassRoom ToClassRoom { get; set; } = null!;

    /// <summary>The status before, so an undo can put it back.</summary>
    public StudentStatus PreviousStatus { get; set; }
}

public static class PromotionRules
{
    /// <summary>A session average below this is flagged for the school to review. Never applied automatically.</summary>
    public const decimal ReviewBelow = 40m;

    public static bool NeedsReview(decimal? sessionAverage) => sessionAverage is { } average && average < ReviewBelow;

    /// <summary>
    /// The class a student moves up to by default: the next level's class with the same arm (Year 3 A → Year 4 A), or that level's
    /// only class. Null when there's no next level (graduate) or the school must choose between several arms.
    /// </summary>
    public static Guid? DefaultTarget(string arm, IReadOnlyCollection<(Guid Id, string Arm)> nextLevelClasses)
    {
        var sameArm = nextLevelClasses.Where(c => string.Equals(c.Arm.Trim(), arm.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (sameArm.Count == 1) return sameArm[0].Id;
        return nextLevelClasses.Count == 1 ? nextLevelClasses.First().Id : null;
    }

    /// <summary>
    /// Promotion opens in the session's third term: from the day it starts (or once the session is over). A future session can't be
    /// promoted from. Returns the error, or null when it's open.
    /// </summary>
    public static DomainError? CheckOpen(AcademicSession session, DateOnly today)
    {
        if (today > session.EndsOn) return null;
        var third = session.Terms.FirstOrDefault(t => t.Number == TermNumber.Third);
        if (third is null)
            return new("promotion.no_third_term", $"Add the third term of {session.Name} first. Promotion opens when it starts.");
        return today >= third.StartsOn ? null
            : new("promotion.too_early", $"Promotion for {session.Name} opens on {third.StartsOn:d MMM yyyy}, when the third term starts.");
    }
}
