using Maydel.Domain.Common;

namespace Maydel.Domain.Transport;

/// <summary>A school bus (E18.1), with the driver who logs its trips.</summary>
public class Bus : Entity
{
    public required string Name { get; set; }
    public string? PlateNumber { get; set; }
    public int Capacity { get; set; }
    public Guid? DriverStaffId { get; set; }
    public bool IsActive { get; set; } = true;

    public DomainError? Check()
    {
        Name = Name.Trim();
        if (Name.Length is 0 or > 80) return new("bus.name", "Give the bus a name (up to 80 characters).", "name");
        if (PlateNumber is { Length: > 20 }) return new("bus.plate", "The plate number is too long.", "plateNumber");
        if (Capacity is < 0 or > 120) return new("bus.capacity", "Capacity must be between 0 and 120.", "capacity");
        return null;
    }
}

public enum RunKind
{
    /// <summary>Morning: collecting children for school.</summary>
    Pickup,

    /// <summary>Afternoon: taking children home.</summary>
    DropOff,
}

/// <summary>One of the day's runs (E18.2), e.g. "Morning pickup – Batch A". The admin renames, adds or retires them.</summary>
public class BusRun : Entity
{
    public required string Name { get; set; }
    public RunKind Kind { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A bus doing a run on a day: when it left and when it got back (WAT, recorded on the driver's tap).</summary>
public class TripLog : Entity
{
    public static readonly TimeSpan UndoWindow = TimeSpan.FromMinutes(2);

    public Guid BusId { get; set; }
    public Guid RunId { get; set; }
    public DateOnly Date { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
    public DateTimeOffset? BackAt { get; set; }
    public string? Note { get; set; }

    /// <summary>Set when the admin corrected the times (with why).</summary>
    public string? Correction { get; set; }

    public DomainError? Leave(DateTimeOffset now)
    {
        if (LeftAt is not null) return new("trip.already_left", "This run has already left. Tap Back when the bus returns.");
        LeftAt = now;
        return null;
    }

    public DomainError? Return(DateTimeOffset now)
    {
        if (LeftAt is null) return new("trip.not_left", "Tap Left when the bus sets off first.");
        if (BackAt is not null) return new("trip.already_back", "This run is already back.");
        if (now < LeftAt) return new("trip.order", "The bus can't be back before it left.");
        BackAt = now;
        return null;
    }

    /// <summary>Takes back the last tap if it was within <see cref="UndoWindow"/> (a mis-tap).</summary>
    public DomainError? Undo(DateTimeOffset now)
    {
        if (BackAt is { } back)
        {
            if (now - back > UndoWindow) return new("trip.undo_window", "Only a tap in the last 2 minutes can be undone. Ask the admin to correct it.");
            BackAt = null;
            return null;
        }
        if (LeftAt is { } left)
        {
            if (now - left > UndoWindow) return new("trip.undo_window", "Only a tap in the last 2 minutes can be undone. Ask the admin to correct it.");
            LeftAt = null;
            return null;
        }
        return new("trip.nothing", "There's nothing to undo.");
    }

    public DomainError? Correct(DateTimeOffset? left, DateTimeOffset? back, string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return new("trip.note", "Say why the times are being changed.", "note");
        if (left is null && back is not null) return new("trip.order", "A return time needs a departure time.", "leftAt");
        if (left is { } l && back is { } b && b < l) return new("trip.order", "The bus must be back after it left.", "backAt");
        (LeftAt, BackAt, Correction) = (left, back, note.Trim());
        return null;
    }
}

public enum CheckKind
{
    /// <summary>Yes or no (flagged when the answer isn't the expected one).</summary>
    YesNo,

    /// <summary>A number, e.g. the odometer reading.</summary>
    Number,

    /// <summary>Free text, e.g. "Any incident?" (flagged when filled in, if set).</summary>
    Text,
}

/// <summary>A question on the driver's daily checklist (E18.3); the admin edits the list.</summary>
public class CheckQuestion : Entity
{
    public required string Text { get; set; }
    public CheckKind Kind { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>YesNo: the answer that's fine (usually yes, e.g. "Tyres OK?").</summary>
    public bool ExpectedYes { get; set; } = true;

    /// <summary>Text: any answer flags the day (e.g. "Any incident?").</summary>
    public bool FlagIfAnswered { get; set; }
}

/// <summary>An answer, with the question's wording at the time (questions can change later).</summary>
public class CheckAnswer
{
    public Guid QuestionId { get; set; }
    public required string Question { get; set; }
    public CheckKind Kind { get; set; }
    public bool? Yes { get; set; }
    public decimal? Number { get; set; }
    public string? Text { get; set; }
    public bool Flagged { get; set; }
}

/// <summary>A driver's checklist for a bus on a day; answering again the same day replaces it.</summary>
public class DailyCheck : Entity
{
    public Guid BusId { get; set; }
    public DateOnly Date { get; set; }
    public Guid DriverUserId { get; set; }
    public required string DriverName { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public List<CheckAnswer> Answers { get; set; } = [];
    public string? Notes { get; set; }
    public bool IsFlagged { get; set; }
}
