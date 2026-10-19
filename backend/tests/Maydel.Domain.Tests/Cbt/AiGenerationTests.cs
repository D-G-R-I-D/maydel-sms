using Maydel.Domain.Cbt;

namespace Maydel.Domain.Tests.Cbt;

public sealed class AiGenerationTests
{
    private static AiGeneration Request() => new()
    {
        Kinds = [QuestionKind.Objective, QuestionKind.Theory, QuestionKind.Objective], Count = 10, Easy = 4, Medium = 4, Hard = 2,
        Files = [new AiSourceFile { Name = "notes.pdf", FilePath = "ai/x.pdf", ContentType = "application/pdf" }],
    };

    [Fact]
    public void A_request_needs_kinds_a_count_the_mix_to_add_up_and_some_material()
    {
        var ok = Request();
        Assert.Null(ok.Check());
        Assert.Equal(2, ok.Kinds.Count);

        var mix = Request();
        mix.Hard = 3;
        Assert.Equal("easy", mix.Check()?.Field);

        var none = Request();
        none.Files = [];
        Assert.Equal("files", none.Check()?.Field);
        none.Guidance = "Fractions: adding unlike denominators";
        Assert.Null(none.Check());

        var many = Request();
        (many.Count, many.Easy, many.Medium, many.Hard) = (41, 41, 0, 0);
        Assert.Equal("count", many.Check()?.Field);
    }

    [Fact]
    public void Five_regenerations_are_allowed_after_the_first_version()
    {
        var g = Request();
        for (var i = 1; i <= 6; i++) g.Versions.Add(new AiVersion { Number = i, Status = GenerationStatus.Ready });
        Assert.Equal((5, false), (g.Regenerations, g.CanRegenerate));
        g.Versions.RemoveAt(5);
        g.Versions.Add(new AiVersion { Number = 6, Status = GenerationStatus.Failed });
        Assert.True(g.CanRegenerate); // a failed attempt doesn't count
    }

    [Fact]
    public void Quotas_are_per_teacher_and_school_per_calendar_month_in_abuja()
    {
        Assert.Null(AiQuota.CanSpend(30, 29, 1000, 10, enabled: true));
        Assert.Equal("ai.quota", AiQuota.CanSpend(30, 30, 1000, 10, enabled: true)?.Code);
        Assert.Equal("ai.school_quota", AiQuota.CanSpend(30, 1, 100, 100, enabled: true)?.Code);
        Assert.Equal("ai.disabled", AiQuota.CanSpend(30, 0, 100, 0, enabled: false)?.Code);

        // 23:30 UTC on 31 October is already 1 November in Abuja.
        var (from, to) = AiQuota.Month(new DateTimeOffset(2026, 10, 31, 23, 30, 0, TimeSpan.Zero));
        Assert.Equal((new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 30, 23, 0, 0, TimeSpan.Zero)), (from, to));
    }
}
