using Maydel.Application.Abstractions;

namespace Maydel.Api.Tests.Infrastructure;

/// <summary>A fixed school "today" (a Thursday in the seeded first term) so register rules don't depend on the real date.</summary>
public sealed class FakeSchoolClock : ISchoolClock
{
    public static readonly DateOnly Default = new(2026, 10, 15);
    public DateOnly Today { get; set; } = Default;

    /// <summary>08:00 in Abuja on <see cref="Default"/>.</summary>
    public DateTimeOffset Now { get; set; } = new(2026, 10, 15, 7, 0, 0, TimeSpan.Zero);
}
