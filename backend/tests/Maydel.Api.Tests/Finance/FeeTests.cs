using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Common;
using Maydel.Application.Finance;
using Maydel.Domain.Common;
using Maydel.Domain.Finance;

namespace Maydel.Api.Tests.Finance;

/// <summary>E10.1–E10.2: fee items, opt-ins, sibling discount, instalment plans and idempotent invoice generation.</summary>
[Collection(nameof(ApiCollection))]
public sealed class FeeTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Invoices_bill_mandatory_and_opted_in_items_minus_the_sibling_discount_once_per_term()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var bursar = await Api.ClientAsync(Roles.Bursar);
        var (_, term, _) = await SessionAsync(admin);
        var level = await LevelOfAsync(admin, s);
        await SettingsAsync(admin, SiblingDiscountMode.Percentage, 10, 20);
        try
        {
            await ItemAsync(bursar, term, "Tuition", 150000, level, mandatory: true, sibling: true);
            var bus = await ItemAsync(bursar, term, "School bus", 20000, level, mandatory: false, sibling: false);
            await ItemAsync(bursar, term, "Exam fee", 5000, null, mandatory: true, sibling: false);
            (await bursar.PutAsJsonAsync($"/api/finance/fee-items/{bus.Id}/opt-ins", new { studentIds = new[] { s.Students[1] } })).EnsureSuccessStatusCode();
            await SiblingsAsync(admin, s.Students[0], s.Students[1]); // same parent; Adaeze was admitted first

            var result = await GenerateAsync(bursar, term, level);

            Assert.True(result.Created >= 3);
            var adaeze = await InvoiceForAsync(bursar, term, s.Students[0]);
            Assert.Equal(["Tuition", "Exam fee"], adaeze.Lines.Select(l => l.Description).Order(StringComparer.Ordinal).Reverse());
            Assert.Equal(155000, adaeze.Total);
            var bola = await InvoiceForAsync(bursar, term, s.Students[1]);
            Assert.Contains(bola.Lines, l => l is { Description: "Sibling discount (2nd child)", Amount: -15000 });
            Assert.Equal(160000, bola.Total);          // 150k + 20k bus + 5k − 15k
            Assert.Equal(155000, (await InvoiceForAsync(bursar, term, s.Students[2])).Total);
            Assert.StartsWith("INV-", bola.Number);
            Assert.Equal(InvoiceStatus.Unpaid, bola.Status);

            var again = await GenerateAsync(bursar, term, level);
            Assert.Equal(0, again.Created);
            Assert.True(again.AlreadyInvoiced >= 3);
        }
        finally
        {
            await SettingsAsync(admin, SiblingDiscountMode.None, 0, 0);
        }
    }

    [Fact]
    public async Task A_terms_own_instalment_plan_overrides_the_sessions_and_splits_each_invoice()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var bursar = await Api.ClientAsync(Roles.Bursar);
        var (year, term, _) = await SessionAsync(admin);
        var level = await LevelOfAsync(admin, s);

        var session = await PlanAsync(admin, term, PlanScope.Session, (60, $"{year}-09-14"), (40, $"{year}-10-30"));
        Assert.Equal((PlanScope.Session, true, 2), (session.Scope, session.Enabled, session.Instalments.Count));
        var own = await PlanAsync(admin, term, PlanScope.Term, (50, $"{year}-09-14"), (30, $"{year}-10-16"), (20, $"{year}-11-20"));
        Assert.Equal(PlanScope.Term, own.Scope);
        Assert.Equal(PlanScope.Term, (await bursar.GetFromJsonAsync<InstalmentPlanDto>($"/api/finance/instalment-plan?termId={term}", Json.Options))!.Scope);

        await ItemAsync(bursar, term, "Tuition", 100000, level, mandatory: true, sibling: false);
        await GenerateAsync(bursar, term, level);
        var invoice = await InvoiceForAsync(bursar, term, s.Students[0]);

        Assert.Equal([50000m, 30000m, 20000m], invoice.Schedule.Select(i => i.Amount));
        Assert.Equal(DateOnly.Parse($"{year}-09-14"), invoice.DueOn);
    }

    [Fact]
    public async Task Plans_need_2_or_3_parts_adding_to_100_and_only_admins_set_them()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (year, term, _) = await SessionAsync(admin);

        var bad = await admin.PutAsJsonAsync("/api/finance/instalment-plan", new
        {
            termId = term, scope = "Term", enabled = true, instalments = new[] { new { percent = 60, dueOn = $"{year}-09-14" }, new { percent = 30, dueOn = $"{year}-10-14" } },
        });
        Assert.True((await bad.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("instalments", out _));
        await (await (await Api.ClientAsync(Roles.Bursar)).PutAsJsonAsync("/api/finance/instalment-plan", new { termId = term, scope = "None", enabled = false, instalments = Array.Empty<object>() }))
            .AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await (await Api.ClientAsync(Roles.Bursar)).PutAsJsonAsync("/api/finance/settings", new { siblingMode = "None", secondChild = 0, thirdAndLater = 0 }))
            .AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Copying_fee_items_brings_the_opt_ins_and_only_into_an_empty_term()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var admin = await Api.ClientAsync(Roles.Admin);
        var bursar = await Api.ClientAsync(Roles.Bursar);
        var (_, first, second) = await SessionAsync(admin);
        var level = await LevelOfAsync(admin, s);
        var bus = await ItemAsync(bursar, first, "School bus", 20000, level, mandatory: false, sibling: false);
        (await bursar.PutAsJsonAsync($"/api/finance/fee-items/{bus.Id}/opt-ins", new { studentIds = new[] { s.Students[0] } })).EnsureSuccessStatusCode();

        var copied = await bursar.PostAsJsonAsync("/api/finance/fee-items/copy", new { fromTermId = first, toTermId = second });

        copied.EnsureSuccessStatusCode();
        var item = Assert.Single((await copied.Content.ReadFromJsonAsync<List<FeeItemDto>>(Json.Options))!);
        Assert.Equal(("School bus", 1), (item.Name, item.OptedIn));
        await (await bursar.PostAsJsonAsync("/api/finance/fee-items/copy", new { fromTermId = first, toTermId = second }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "fee.term_has_items");
    }

    [Fact]
    public async Task Fee_items_are_validated_and_mandatory_items_take_no_opt_ins()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var bursar = await Api.ClientAsync(Roles.Bursar);
        var (_, term, _) = await SessionAsync(admin);

        var zero = await bursar.PostAsJsonAsync("/api/finance/fee-items", new { termId = term, name = "Free", amount = 0, isMandatory = true });
        Assert.True((await zero.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("amount", out _));
        var tuition = await ItemAsync(bursar, term, "Tuition", 1000, null, mandatory: true, sibling: false);
        await (await bursar.PutAsJsonAsync($"/api/finance/fee-items/{tuition.Id}/opt-ins", new { studentIds = Array.Empty<Guid>() }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "fee.mandatory");
    }

    [Theory]
    [InlineData(Roles.Teacher)]
    [InlineData(Roles.Parent)]
    [InlineData(Roles.Student)]
    public async Task Only_admins_and_bursars_see_finance(string role)
    {
        var client = await Api.ClientAsync(role);
        await (await client.GetAsync("/api/finance/invoices")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.GetAsync("/api/finance/settings")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    private static int _year = 2100 + Random.Shared.Next(0, 700);
    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);

    /// <summary>A fresh far-future session with first and second terms, so fee items and invoices never clash between tests.</summary>
    private static async Task<(int Year, Guid First, Guid Second)> SessionAsync(HttpClient admin)
    {
        var year = Interlocked.Increment(ref _year);
        var created = await admin.PostAsJsonAsync("/api/academics/sessions", new { name = $"{year}/{year + 1}", startsOn = $"{year}-09-14", endsOn = $"{year + 1}-07-23" });
        created.EnsureSuccessStatusCode();
        var session = (await created.Content.ReadFromJsonAsync<SessionDto>(Json.Options))!;
        (await admin.PostAsJsonAsync($"/api/academics/sessions/{session.Id}/terms", new { number = "First", startsOn = $"{year}-09-14", endsOn = $"{year}-12-11" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/academics/sessions/{session.Id}/terms", new { number = "Second", startsOn = $"{year + 1}-01-11", endsOn = $"{year + 1}-03-26" })).EnsureSuccessStatusCode();
        var terms = (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!.Single(x => x.Id == session.Id).Terms;
        return (year, terms.Single(t => t.Number == Domain.Academics.TermNumber.First).Id, terms.Single(t => t.Number == Domain.Academics.TermNumber.Second).Id);
    }

    private static async Task<Guid> LevelOfAsync(HttpClient admin, School s) =>
        (await admin.GetFromJsonAsync<List<ClassRoomDto>>("/api/academics/classrooms", Json.Options))!.Single(r => r.Id == s.ClassRoomId).ClassLevelId;

    private static async Task SettingsAsync(HttpClient admin, SiblingDiscountMode mode, decimal second, decimal third) =>
        (await admin.PutAsJsonAsync("/api/finance/settings", new { siblingMode = mode.ToString(), secondChild = second, thirdAndLater = third })).EnsureSuccessStatusCode();

    private static async Task<FeeItemDto> ItemAsync(HttpClient client, Guid term, string name, decimal amount, Guid? level, bool mandatory, bool sibling)
    {
        var response = await client.PostAsJsonAsync("/api/finance/fee-items", new { termId = term, name, amount, classLevelId = level, isMandatory = mandatory, siblingDiscount = sibling });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FeeItemDto>(Json.Options))!;
    }

    private static async Task SiblingsAsync(HttpClient admin, params Guid[] students)
    {
        var phone = $"0805{Interlocked.Increment(ref _phone):0000000}";
        foreach (var id in students)
            (await admin.PostAsJsonAsync($"/api/students/{id}/guardians",
                new { fullName = "Mrs. Sibling Parent", phone, relationship = "Mother", isPrimaryContact = true, canPickUp = true })).EnsureSuccessStatusCode();
    }

    private static async Task<GenerateInvoicesResult> GenerateAsync(HttpClient client, Guid term, Guid level)
    {
        var response = await client.PostAsJsonAsync("/api/finance/invoices/generate", new { termId = term, classLevelId = level });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GenerateInvoicesResult>(Json.Options))!;
    }

    private static async Task<InvoiceDetail> InvoiceForAsync(HttpClient client, Guid term, Guid studentId)
    {
        var page = (await client.GetFromJsonAsync<PagedResult<InvoiceSummary>>($"/api/finance/invoices?termId={term}&pageSize=100", Json.Options))!;
        var summary = page.Items.Single(i => i.StudentId == studentId);
        return (await client.GetFromJsonAsync<InvoiceDetail>($"/api/finance/invoices/{summary.Id}", Json.Options))!;
    }

    private static async Task<InstalmentPlanDto> PlanAsync(HttpClient admin, Guid term, PlanScope scope, params (decimal Percent, string DueOn)[] parts)
    {
        var response = await admin.PutAsJsonAsync("/api/finance/instalment-plan", new
        {
            termId = term, scope = scope.ToString(), enabled = true, instalments = parts.Select(p => new { percent = p.Percent, dueOn = p.DueOn }),
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InstalmentPlanDto>(Json.Options))!;
    }
}
