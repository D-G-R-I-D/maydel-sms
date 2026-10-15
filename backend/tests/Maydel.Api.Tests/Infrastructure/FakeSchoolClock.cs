using Maydel.Application.Abstractions;

namespace Maydel.Api.Tests.Infrastructure;

/// <summary>A fixed school "today" (a Thursday in the seeded first term) so register rules don't depend on the real date.</summary>
public sealed class FakeSchoolClock : ISchoolClock
{
    public static readonly DateOnly Default = new(2026, 10, 15);
    public DateOnly Today { get; set; } = Default;
}
