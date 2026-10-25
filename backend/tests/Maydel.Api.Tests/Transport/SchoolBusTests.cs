using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Identity;
using Maydel.Application.Notifications;
using Maydel.Application.StaffMembers;
using Maydel.Application.Transport;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Maydel.Domain.Transport;

namespace Maydel.Api.Tests.Transport;

/// <summary>E18: drivers tap Left/Back for each run and fill in the daily checklist; the admin sees the day and corrects.</summary>
[Collection(nameof(ApiCollection))]
public sealed class SchoolBusTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task A_driver_logs_the_morning_runs_and_the_admin_sees_them_on_the_board()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (driver, staff) = await DriverAsync();
        var bus = await ReadAsync<BusDto>(await admin.PostAsJsonAsync("/api/transport/buses", new { name = $"Bus {Guid.NewGuid():N}"[..12], plateNumber = "abj 123 xy", capacity = 30, driverStaffId = staff.Id }));
        Assert.Equal(("ABJ 123 XY", staff.FullName), (bus.PlateNumber, bus.DriverName));

        var today = await ReadAsync<DriverToday>(await driver.GetAsync("/api/driver/today"));
        var day = Assert.Single(today.Buses);
        Assert.True(day.Trips.Count >= 4); // the four default runs
        Assert.NotEmpty(today.Questions);
        var batchA = day.Trips.First(t => t.Run.Contains("Batch A") && t.Kind == RunKind.Pickup);

        var original = Api.Clock.Now;
        try
        {
            var left = await ReadAsync<TripDto>(await driver.PostAsJsonAsync("/api/driver/trips", new { busId = bus.Id, runId = batchA.RunId, @event = "Left" }));
            Assert.True(left.CanUndo);
            await (await driver.PostAsJsonAsync("/api/driver/trips", new { busId = bus.Id, runId = batchA.RunId, @event = "Left" })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "trip.already_left");

            // A mis-tap can be undone within two minutes, then re-tapped.
            Assert.Null((await ReadAsync<TripDto>(await driver.PostAsJsonAsync("/api/driver/trips/undo", new { busId = bus.Id, runId = batchA.RunId }))).LeftAt);
            (await driver.PostAsJsonAsync("/api/driver/trips", new { busId = bus.Id, runId = batchA.RunId, @event = "Left" })).EnsureSuccessStatusCode();
            Api.Clock.Now = Api.Clock.Now.AddMinutes(45);
            var back = await ReadAsync<TripDto>(await driver.PostAsJsonAsync("/api/driver/trips", new { busId = bus.Id, runId = batchA.RunId, @event = "Back" }));
            Assert.Equal(45, back.Minutes);
            Api.Clock.Now = Api.Clock.Now.AddMinutes(5);
            await (await driver.PostAsJsonAsync("/api/driver/trips/undo", new { busId = bus.Id, runId = batchA.RunId })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "trip.undo_window");
        }
        finally
        {
            Api.Clock.Now = original;
        }

        var board = await ReadAsync<BusDayBoard>(await admin.GetAsync("/api/transport/board"));
        var row = Assert.Single(board.Buses, b => b.Bus.Id == bus.Id).Trips.Single(t => t.RunId == batchA.RunId);
        Assert.Equal(45, row.Minutes);

        // The admin corrects a time (with a reason) and exports the range.
        var fixedTrip = await ReadAsync<TripDto>(await admin.PutAsJsonAsync($"/api/transport/trips/{bus.Id}/{batchA.RunId}/{board.Date:yyyy-MM-dd}",
            new { leftAt = row.LeftAt!.Value.AddMinutes(-10), backAt = row.BackAt, note = "Driver tapped late" }));
        Assert.Equal((55, "Driver tapped late"), (fixedTrip.Minutes, fixedTrip.Correction));
        await (await admin.PutAsJsonAsync($"/api/transport/trips/{bus.Id}/{batchA.RunId}/{board.Date:yyyy-MM-dd}", new { leftAt = row.LeftAt, note = "" }))
            .AssertProblemAsync(HttpStatusCode.BadRequest);
        var csv = await admin.GetStringAsync($"/api/transport/trips.csv?from={board.Date:yyyy-MM-dd}&to={board.Date:yyyy-MM-dd}&busId={bus.Id}");
        Assert.Contains("Driver tapped late", csv);
    }

    [Fact]
    public async Task A_check_with_a_problem_is_flagged_for_the_admin()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (driver, staff) = await DriverAsync();
        var bus = await ReadAsync<BusDto>(await admin.PostAsJsonAsync("/api/transport/buses", new { name = $"Bus {Guid.NewGuid():N}"[..12], capacity = 18, driverStaffId = staff.Id }));
        var questions = (await ReadAsync<DriverToday>(await driver.GetAsync("/api/driver/today"))).Questions;

        object Answer(CheckQuestionDto q, bool problem) => q.Kind switch
        {
            CheckKind.YesNo => new { questionId = q.Id, yes = problem && q.Text.StartsWith("Tyres") ? !q.ExpectedYes : q.ExpectedYes },
            CheckKind.Number => new { questionId = q.Id, number = 48213.5m },
            _ => new { questionId = q.Id, text = problem && q.FlagIfAnswered ? "Rear tyre looks flat" : null },
        };

        await (await driver.PostAsJsonAsync("/api/driver/checks", new { busId = bus.Id, answers = Array.Empty<object>() })).AssertProblemAsync(HttpStatusCode.BadRequest);
        var ok = await ReadAsync<CheckDto>(await driver.PostAsJsonAsync("/api/driver/checks", new { busId = bus.Id, answers = questions.Select(q => Answer(q, false)) }));
        Assert.False(ok.IsFlagged);

        var flagged = await ReadAsync<CheckDto>(await driver.PostAsJsonAsync("/api/driver/checks", new { busId = bus.Id, answers = questions.Select(q => Answer(q, true)), notes = "Will fix at lunch" }));
        Assert.True(flagged.IsFlagged);
        Assert.Contains(flagged.Answers, a => a.Flagged && a.Text == "Rear tyre looks flat");
        Assert.Contains((await ReadAsync<List<CheckDto>>(await admin.GetAsync($"/api/transport/checks?flaggedOnly=true&busId={bus.Id}"))), c => c.Id == flagged.Id);
        Assert.Contains((await admin.GetFromJsonAsync<NotificationInbox>("/api/notifications", Json.Options))!.Items, n => n.Kind == NotificationKind.BusCheckFlagged);
    }

    [Fact]
    public async Task Drivers_only_log_their_own_buses_and_never_see_students()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (driver, _) = await DriverAsync();
        var (_, otherStaff) = await DriverAsync();
        var theirs = await ReadAsync<BusDto>(await admin.PostAsJsonAsync("/api/transport/buses", new { name = $"Bus {Guid.NewGuid():N}"[..12], capacity = 20, driverStaffId = otherStaff.Id }));
        var run = (await ReadAsync<List<RunDto>>(await admin.GetAsync("/api/transport/runs")))[0];

        await (await driver.PostAsJsonAsync("/api/driver/trips", new { busId = theirs.Id, runId = run.Id, @event = "Left" })).AssertProblemAsync(HttpStatusCode.NotFound, "bus.not_yours");
        await (await driver.GetAsync("/api/students")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await driver.GetAsync("/api/transport/board")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await (await Api.ClientAsync(Roles.Teacher)).GetAsync("/api/driver/today")).AssertProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.OK, (await driver.GetAsync("/api/clock/me")).StatusCode); // drivers clock in like other staff
    }

    // ---------- helpers ----------

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;
    }

    private async Task<(HttpClient Client, StaffDetail Staff)> DriverAsync()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var email = ApiFactory.UniqueEmail();
        var created = (await (await admin.PostAsJsonAsync("/api/staff", new { fullName = "Driver " + email[..6], email, roles = new[] { "Driver" } }))
            .Content.ReadFromJsonAsync<StaffCreated>(Json.Options))!;
        var first = await Api.LoginAsync(email, created.Credentials!.TemporaryPassword);
        var changed = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = created.Credentials.TemporaryPassword, newPassword = "Bus#NewPass2026" });
        return (Api.CreateClient((await changed.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!.AccessToken), created.Staff);
    }
}
