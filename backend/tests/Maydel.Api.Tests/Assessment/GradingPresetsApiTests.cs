using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Assessment;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Assessment;

[Collection(nameof(ApiCollection))]
public sealed class GradingPresetsApiTests(PostgresFixture fixture)
{
    [Fact]
    public async Task New_installations_default_to_the_school_standard_scale_and_both_presets_are_offered()
    {
        var admin = await fixture.Api.ClientAsync(Roles.Admin);

        var settings = (await admin.GetFromJsonAsync<AssessmentSettings>("/api/assessment/settings", Json.Options))!;
        var presets = (await admin.GetFromJsonAsync<List<GradingPresetDto>>("/api/assessment/grading-presets", Json.Options))!;

        Assert.Equal(["A+", "A", "B+", "B", "C+", "C", "D", "F"], settings.Bands.Select(b => b.Grade));
        Assert.Equal(["school-standard", "simple"], presets.Select(p => p.Key));
        Assert.Equal(settings.Bands, presets[0].Bands);
    }

    [Theory]
    [InlineData(Roles.Bursar)]
    [InlineData(Roles.Parent)]
    public async Task Presets_are_for_admins_and_teachers(string role) =>
        await (await (await fixture.Api.ClientAsync(role)).GetAsync("/api/assessment/grading-presets")).AssertProblemAsync(HttpStatusCode.Forbidden);
}
