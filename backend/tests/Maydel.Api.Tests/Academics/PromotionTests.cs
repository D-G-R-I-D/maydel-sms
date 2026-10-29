using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Identity;
using Maydel.Application.Promotions;
using Maydel.Application.Students;
using Maydel.Domain.Academics;
using Maydel.Domain.Common;
using Maydel.Domain.People;

namespace Maydel.Api.Tests.Academics;

/// <summary>E3.6: end-of-session promotion. Preview, then apply with exceptions in one go; never twice; undo per class.</summary>
[Collection(nameof(ApiCollection))]
public sealed class PromotionTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task A_class_moves_up_with_exceptions_once_and_can_be_undone()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var session = await PastSessionAsync(admin);
        var levels = await LevelsAsync(admin);
        var arm = Arm();
        var from = await ClassAsync(admin, levels[3], arm);
        var to = await ClassAsync(admin, levels[4], arm);
        var kids = new[] { await StudentAsync(admin, from, "Ada"), await StudentAsync(admin, from, "Bayo"), await StudentAsync(admin, from, "Chi") };

        // The preview suggests moving everyone up to the next level's same arm.
        var plan = await PlanAsync(admin, session, from);
        Assert.True(plan.CanPromote);
        var cls = Assert.Single(plan.Classes);
        Assert.Equal((to, false), (cls.NextClassRoomId, cls.IsFinalLevel));
        Assert.All(cls.Students, s => Assert.Equal((PromotionOutcome.Promote, (Guid?)to, (PromotionDone?)null), (s.SuggestedOutcome, s.SuggestedClassRoomId, s.Done)));

        // Applied with exceptions: one repeats, one leaves.
        var summary = await (await admin.PostAsJsonAsync("/api/promotions", new
        {
            sessionId = session,
            decisions = new object[]
            {
                new { studentId = kids[0], outcome = "Promote", toClassRoomId = to },
                new { studentId = kids[1], outcome = "Repeat" },
                new { studentId = kids[2], outcome = "Withdraw" },
            },
        })).Content.ReadFromJsonAsync<PromotionSummary>(Json.Options);
        Assert.Equal(new PromotionSummary(1, 1, 0, 1), summary);
        Assert.Equal((to, StudentStatus.Active), await StateAsync(admin, kids[0]));
        Assert.Equal((from, StudentStatus.Active), await StateAsync(admin, kids[1]));
        Assert.Equal((from, StudentStatus.Withdrawn), await StateAsync(admin, kids[2]));

        // Not twice in a session; the preview shows what was done.
        await (await admin.PostAsJsonAsync("/api/promotions", new { sessionId = session, decisions = new[] { new { studentId = kids[1], outcome = "Promote", toClassRoomId = to } } }))
            .AssertProblemAsync(HttpStatusCode.Conflict, "promotion.already_done");
        Assert.All(Assert.Single((await PlanAsync(admin, session, from)).Classes).Students, s => Assert.NotNull(s.Done));

        // Undo puts everyone back as they were.
        var undone = await (await admin.PostAsJsonAsync("/api/promotions/undo", new { sessionId = session, classRoomId = from }))
            .Content.ReadFromJsonAsync<PromotionUndone>(Json.Options);
        Assert.Equal(3, undone!.Undone);
        foreach (var k in kids) Assert.Equal((from, StudentStatus.Active), await StateAsync(admin, k));
    }

    [Fact]
    public async Task The_top_class_graduates_and_a_full_class_stops_the_whole_run()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var session = await PastSessionAsync(admin);
        var levels = await LevelsAsync(admin);

        var top = await ClassAsync(admin, levels[^1], Arm());
        var leaver = await StudentAsync(admin, top, "Dayo");
        var topClass = Assert.Single((await PlanAsync(admin, session, top)).Classes);
        Assert.True(topClass.IsFinalLevel);
        Assert.Equal(PromotionOutcome.Graduate, Assert.Single(topClass.Students).SuggestedOutcome);
        (await admin.PostAsJsonAsync("/api/promotions", new { sessionId = session, decisions = new[] { new { studentId = leaver, outcome = "Graduate" } } })).EnsureSuccessStatusCode();
        Assert.Equal((top, StudentStatus.Graduated), await StateAsync(admin, leaver));

        var arm = Arm();
        var from = await ClassAsync(admin, levels[5], arm);
        var small = await ClassAsync(admin, levels[6], arm, capacity: 1);
        var a = await StudentAsync(admin, from, "Efe");
        var b = await StudentAsync(admin, from, "Femi");
        await (await admin.PostAsJsonAsync("/api/promotions", new
        {
            sessionId = session,
            decisions = new[] { new { studentId = a, outcome = "Promote", toClassRoomId = small }, new { studentId = b, outcome = "Promote", toClassRoomId = small } },
        })).AssertProblemAsync((HttpStatusCode)422, "promotion.class_full");
        Assert.Equal((from, StudentStatus.Active), await StateAsync(admin, a)); // nobody moved
    }

    [Fact]
    public async Task Only_admins_and_only_from_the_third_term()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var s = await SchoolBuilder.SetUpAsync(Api);
        await (await s.FormTeacher.GetAsync("/api/promotions")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await (await Api.ClientAsync(Roles.Bursar)).PostAsJsonAsync("/api/promotions", new { decisions = Array.Empty<object>() }))
            .AssertProblemAsync(HttpStatusCode.Forbidden);

        // The test clock is in the first term of the current session.
        var plan = (await admin.GetFromJsonAsync<PromotionPlan>($"/api/promotions?classRoomId={s.ClassRoomId}", Json.Options))!;
        Assert.False(plan.CanPromote);
        Assert.NotNull(plan.ClosedReason);
        await (await admin.PostAsJsonAsync("/api/promotions", new { decisions = new[] { new { studentId = s.Students[0], outcome = "Repeat" } } }))
            .AssertProblemAsync((HttpStatusCode)422);
        await (await admin.PostAsJsonAsync("/api/promotions", new { decisions = Array.Empty<object>() })).AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    // ---------- helpers ----------

    private static string Arm() => "P" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>A finished session, so promotion is open whatever the test clock says.</summary>
    private static async Task<Guid> PastSessionAsync(HttpClient admin)
    {
        await admin.PostAsJsonAsync("/api/academics/sessions", new { name = "2000/2001", startsOn = "2000-09-11", endsOn = "2001-07-20" });
        return (await admin.GetFromJsonAsync<List<SessionDto>>("/api/academics/sessions", Json.Options))!.Single(x => x.Name == "2000/2001").Id;
    }

    private static async Task<List<ClassLevelDto>> LevelsAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<ClassLevelDto>>("/api/academics/class-levels", Json.Options))!;

    private static async Task<Guid> ClassAsync(HttpClient admin, ClassLevelDto level, string arm, int capacity = 30)
    {
        var r = await admin.PostAsJsonAsync("/api/academics/classrooms", new { classLevelId = level.Id, arm, capacity });
        return (await r.Content.ReadFromJsonAsync<ClassRoomDto>(Json.Options))!.Id;
    }

    private static async Task<Guid> StudentAsync(HttpClient admin, Guid classRoomId, string name)
    {
        var r = await admin.PostAsJsonAsync("/api/students", new { firstName = name, lastName = "Promo", dateOfBirth = "2015-03-10", classRoomId, admittedOn = "2026-09-14" });
        return (await r.Content.ReadFromJsonAsync<StudentDetail>(Json.Options))!.Id;
    }

    private static async Task<PromotionPlan> PlanAsync(HttpClient admin, Guid session, Guid classRoomId) =>
        (await admin.GetFromJsonAsync<PromotionPlan>($"/api/promotions?sessionId={session}&classRoomId={classRoomId}", Json.Options))!;

    private static async Task<(Guid?, StudentStatus)> StateAsync(HttpClient admin, Guid id)
    {
        var s = (await admin.GetFromJsonAsync<StudentDetail>($"/api/students/{id}", Json.Options))!;
        return (s.ClassRoomId, s.Status);
    }
}
