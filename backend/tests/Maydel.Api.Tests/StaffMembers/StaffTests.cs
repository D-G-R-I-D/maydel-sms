using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Common;
using Maydel.Application.Identity;
using Maydel.Application.StaffMembers;
using Maydel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Api.Tests.StaffMembers;

/// <summary>E4.1 — staff records and logins; E1.5 admin password reset.</summary>
[Collection(nameof(ApiCollection))]
public sealed class StaffTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Admin_creates_staff_whose_temporary_password_works_once_and_must_be_changed()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var email = ApiFactory.UniqueEmail();

        var response = await admin.PostAsJsonAsync("/api/staff", new { fullName = "Mrs. Ngozi Obi", email = email.ToUpperInvariant(), phone = "+234 803 111 2222", jobTitle = "Year 4 Teacher", roles = new[] { "Teacher" } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<StaffCreated>(Json.Options))!;
        Assert.False(created.LinkedExistingAccount);
        Assert.Equal(email, created.Credentials!.UserName); // stored lower-case
        Assert.Matches("^MSS-STF-\\d{3,}$", created.Staff.StaffNumber);
        Assert.Equal([Role.Teacher], created.Staff.Roles);
        Assert.True(created.Staff.MustChangePassword);

        var session = await Api.LoginAsync(email, created.Credentials.TemporaryPassword);
        Assert.True(session.User.MustChangePassword);

        // Shown once: reading the staff member never returns a password.
        var detail = await admin.GetStringAsync($"/api/staff/{created.Staff.Id}");
        Assert.DoesNotContain(created.Credentials.TemporaryPassword, detail);
    }

    [Fact]
    public async Task An_existing_parent_account_is_linked_instead_of_duplicated()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var email = ApiFactory.UniqueEmail();
        var parent = await Api.CreateUserAsync(Roles.Parent, email);

        var response = await admin.PostAsJsonAsync("/api/staff", new { fullName = "Parent Turned Teacher", email, roles = new[] { "Teacher" } });

        var created = (await response.Content.ReadFromJsonAsync<StaffCreated>(Json.Options))!;
        Assert.True(created.LinkedExistingAccount);
        Assert.Null(created.Credentials);
        var me = (await Api.LoginAsync(email)).User; // their existing password still works
        Assert.Equal(parent.Id, me.Id);
        Assert.Equal([Role.Teacher, Role.Parent], me.Roles);
    }

    [Fact]
    public async Task Duplicate_staff_email_and_student_accounts_are_refused()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var email = ApiFactory.UniqueEmail();
        (await admin.PostAsJsonAsync("/api/staff", new { fullName = "First", email, roles = new[] { "Bursar" } })).EnsureSuccessStatusCode();

        await (await admin.PostAsJsonAsync("/api/staff", new { fullName = "Second", email, roles = new[] { "Teacher" } }))
            .AssertProblemAsync(HttpStatusCode.Conflict, "staff.duplicate_email");

        var studentEmail = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Student, studentEmail);
        await (await admin.PostAsJsonAsync("/api/staff", new { fullName = "Student", email = studentEmail, roles = new[] { "Teacher" } }))
            .AssertProblemAsync(HttpStatusCode.Conflict, "staff.email_is_student");
    }

    [Fact]
    public async Task Staff_roles_must_be_admin_teacher_or_bursar()
    {
        var admin = await Api.ClientAsync(Roles.Admin);

        var parentRole = await admin.PostAsJsonAsync("/api/staff", new { fullName = "X", email = ApiFactory.UniqueEmail(), roles = new[] { "Parent" } });
        var none = await admin.PostAsJsonAsync("/api/staff", new { fullName = "X", email = ApiFactory.UniqueEmail(), roles = Array.Empty<string>() });
        var badEmail = await admin.PostAsJsonAsync("/api/staff", new { fullName = "X", email = "not-an-email", roles = new[] { "Teacher" } });

        Assert.True((await parentRole.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("roles", out _));
        Assert.True((await none.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("roles", out _));
        Assert.True((await badEmail.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Changing_roles_takes_effect_on_the_next_request()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (staff, email, password) = await CreateStaffAsync(admin, "Teacher");
        var teacherToken = (await LoginAndChangePasswordAsync(email, password)).AccessToken;

        var update = await admin.PutAsJsonAsync($"/api/staff/{staff.Id}", new { fullName = staff.FullName, email, roles = new[] { "Bursar" }, isActive = true });
        update.EnsureSuccessStatusCode();
        Assert.Equal([Role.Bursar], (await update.Content.ReadFromJsonAsync<StaffDetail>(Json.Options))!.Roles);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient(teacherToken).GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal([Role.Bursar], (await Api.LoginAsync(email, NewPassword)).User.Roles);
    }

    [Fact]
    public async Task Deactivating_staff_cuts_off_their_login_immediately()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (staff, email, password) = await CreateStaffAsync(admin, "Teacher");
        var session = await LoginAndChangePasswordAsync(email, password);

        (await admin.PutAsJsonAsync($"/api/staff/{staff.Id}", new { fullName = staff.FullName, email, roles = new[] { "Teacher" }, isActive = false })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient(session.AccessToken).GetAsync("/api/auth/me")).StatusCode);
        await (await Api.CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = email, password = NewPassword }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");

        // Inactive staff disappear from pickers.
        var lookup = (await admin.GetFromJsonAsync<List<StaffLookupItem>>("/api/staff/lookup", Json.Options))!;
        Assert.DoesNotContain(lookup, s => s.Id == staff.Id);
    }

    [Fact]
    public async Task Admins_cannot_lock_themselves_out()
    {
        var email = ApiFactory.UniqueEmail();
        var admin = await Api.ClientAsync(Roles.Admin);
        var (staff, _, password) = await CreateStaffAsync(admin, "Admin", email);
        var self = Api.CreateClient((await LoginAndChangePasswordAsync(email, password)).AccessToken);

        await (await self.PutAsJsonAsync($"/api/staff/{staff.Id}", new { fullName = staff.FullName, email, roles = new[] { "Teacher" }, isActive = true }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "staff.self_lockout");
        await (await self.PutAsJsonAsync($"/api/staff/{staff.Id}", new { fullName = staff.FullName, email, roles = new[] { "Admin" }, isActive = false }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "staff.self_lockout");
        await (await self.PostAsync($"/api/staff/{staff.Id}/reset-password", null))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "staff.self_reset");

        // Editing other details of yourself is fine.
        (await self.PutAsJsonAsync($"/api/staff/{staff.Id}", new { fullName = "Renamed Admin", email, jobTitle = "Principal", roles = new[] { "Admin", "Teacher" }, isActive = true })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Password_reset_issues_a_new_temporary_password_and_signs_out_everywhere()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (staff, email, password) = await CreateStaffAsync(admin, "Teacher");
        var old = await LoginAndChangePasswordAsync(email, password);

        var reset = await admin.PostAsync($"/api/staff/{staff.Id}/reset-password", null);
        reset.EnsureSuccessStatusCode();
        var credentials = (await reset.Content.ReadFromJsonAsync<IssuedCredentials>(Json.Options))!;

        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient(old.AccessToken).GetAsync("/api/auth/me")).StatusCode);
        await (await Api.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = old.RefreshToken }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.session_expired");
        var fresh = await Api.LoginAsync(email, credentials.TemporaryPassword);
        Assert.True(fresh.User.MustChangePassword);
    }

    [Fact]
    public async Task Password_reset_also_unlocks_a_locked_account()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var (staff, email, _) = await CreateStaffAsync(admin, "Teacher");
        for (var i = 0; i < 5; i++)
            await Api.CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = email, password = "Wrong#Pass123" });

        var credentials = (await (await admin.PostAsync($"/api/staff/{staff.Id}/reset-password", null)).Content.ReadFromJsonAsync<IssuedCredentials>(Json.Options))!;

        await Api.LoginAsync(email, credentials.TemporaryPassword);
    }

    [Fact]
    public async Task List_searches_by_name_email_or_staff_number_and_shows_assignments()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (staff, _, _) = await CreateStaffAsync(admin, "Teacher", name: $"Searchable {tag}");

        var page = (await admin.GetFromJsonAsync<PagedResult<StaffListItem>>($"/api/staff?search={tag.ToUpperInvariant()}", Json.Options))!;
        Assert.Equal(staff.Id, Assert.Single(page.Items).Id);
        page = (await admin.GetFromJsonAsync<PagedResult<StaffListItem>>($"/api/staff?search={staff.StaffNumber}", Json.Options))!;
        Assert.Contains(page.Items, s => s.Id == staff.Id);

        // The demo form teacher's assignments show on their record.
        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<Maydel.Infrastructure.Persistence.AppDbContext>();
            var room = db.ClassRooms.First();
            room.FormTeacherId = staff.Id;
            await db.SaveChangesAsync();
        });
        var detail = (await admin.GetFromJsonAsync<StaffDetail>($"/api/staff/{staff.Id}", Json.Options))!;
        Assert.Contains(detail.Assignments, a => a.SubjectId == null);
    }

    public static TheoryData<string> NonAdmins => [Roles.Teacher, Roles.Bursar, Roles.Parent, Roles.Student];

    [Theory]
    [MemberData(nameof(NonAdmins))]
    public async Task Only_admins_manage_staff(string role)
    {
        var client = await Api.ClientAsync(role);
        var id = Guid.NewGuid();

        await (await client.GetAsync("/api/staff")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.GetAsync($"/api/staff/{id}")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PostAsJsonAsync("/api/staff", new { fullName = "X", email = "x@y.z", roles = new[] { "Admin" } })).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PutAsJsonAsync($"/api/staff/{id}", new { fullName = "X", email = "x@y.z", roles = new[] { "Admin" }, isActive = true })).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PostAsync($"/api/staff/{id}/reset-password", null)).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(Roles.Teacher, HttpStatusCode.OK)]
    [InlineData(Roles.Bursar, HttpStatusCode.OK)]
    [InlineData(Roles.Parent, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Student, HttpStatusCode.Forbidden)]
    public async Task Staff_lookup_is_for_staff_only(string role, HttpStatusCode expected)
    {
        var client = await Api.ClientAsync(role);
        Assert.Equal(expected, (await client.GetAsync("/api/staff/lookup")).StatusCode);
    }

    // ---------- helpers ----------

    private const string NewPassword = "Brand#NewPass2026";

    private static async Task<(StaffDetail Staff, string Email, string Password)> CreateStaffAsync(
        HttpClient admin, string role, string? email = null, string? name = null)
    {
        email ??= ApiFactory.UniqueEmail();
        var response = await admin.PostAsJsonAsync("/api/staff", new { fullName = name ?? $"Staff {Guid.NewGuid():N}"[..20], email, roles = new[] { role } });
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<StaffCreated>(Json.Options))!;
        return (created.Staff, email, created.Credentials!.TemporaryPassword);
    }

    private async Task<AuthResponse> LoginAndChangePasswordAsync(string email, string temporaryPassword)
    {
        var first = await Api.LoginAsync(email, temporaryPassword);
        var change = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = temporaryPassword, newPassword = NewPassword });
        change.EnsureSuccessStatusCode();
        return (await change.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
    }
}
