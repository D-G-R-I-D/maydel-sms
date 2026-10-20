namespace Maydel.Infrastructure.Persistence;

/// <summary>A one-time seed that has run (e.g. "site-starter-v1"), so it never runs again: what admins delete stays deleted.</summary>
public sealed class SeedRun
{
    public required string Key { get; set; }
    public DateTimeOffset RanAt { get; set; }
}
