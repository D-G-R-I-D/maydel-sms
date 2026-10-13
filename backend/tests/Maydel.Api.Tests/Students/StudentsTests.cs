using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Common;
using Maydel.Application.Identity;
using Maydel.Application.Students;
using Maydel.Domain.Common;
using Maydel.Domain.People;

namespace Maydel.Api.Tests.Students;

/// <summary>E3.1–E3.5 — students, guardians and their logins.</summary>
[Collection(nameof(ApiCollection))]
public sealed class StudentsTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;
    private const string NewPassword = "Brand#NewPass2026";

    // ---------- E3.1 create / edit ----------

    [Fact]
    public async Task Admission_numbers_are_generated_per_admission_year_in_sequence()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var room = await CreateClassAsync(admin);
        var year = Random.Shared.Next(1960, 2020); // a past year no other test admits into

        var first = await CreateStudentAsync(admin, room.Id, admittedOn: $"{year}-01-10", dob: $"{year - 8}-03-10");
        var second = await CreateStudentAsync(admin, room.Id, admittedOn: $"{year}-02-10", dob: $"{year - 8}-03-10");

        Assert.Equal($"MSS/{year}/0001", first.AdmissionNumber);
        Assert.Equal($"MSS/{year}/0002", second.AdmissionNumber);
        Assert.Equal(room.Name, first.ClassRoom);
        Assert.Equal(StudentStatus.Active, first.Status);
        Assert.False(first.HasLogin);
    }

    [Fact]
    public async Task A_full_class_refuses_another_student()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var room = await CreateClassAsync(admin, capacity: 1);
        await CreateStudentAsync(admin, room.Id);

        var response = await admin.PostAsJsonAsync("/api/students", StudentBody(room.Id));

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "classroom.full");
    }

    [Fact]
    public async Task Biodata_rules_come_back_as_field_errors()
    {
        var admin = await Api.ClientAsync(Roles.Admin);

        var futureBirth = await admin.PostAsJsonAsync("/api/students", StudentBody(null, dob: "2099-01-01"));
        var missingNames = await admin.PostAsJsonAsync("/api/students", new { admittedOn = "2026-09-14" });

        Assert.True((await futureBirth.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("dateOfBirth", out _));
        var errors = (await missingNames.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors");
        Assert.True(errors.TryGetProperty("firstName", out _));
        Assert.True(errors.TryGetProperty("lastName", out _));
    }

    [Fact]
    public async Task Admin_edits_biodata_and_moves_class_but_the_admission_number_stays()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var from = await CreateClassAsync(admin);
        var to = await CreateClassAsync(admin);
        var student = await CreateStudentAsync(admin, from.Id);

        var response = await admin.PutAsJsonAsync($"/api/students/{student.Id}", StudentBody(to.Id, first: "Chiamaka", house: "Blue"));

        var updated = (await response.Content.ReadFromJsonAsync<StudentDetail>(Json.Options))!;
        Assert.Equal("Chiamaka", updated.FirstName);
        Assert.Equal("Blue", updated.House);
        Assert.Equal(to.Name, updated.ClassRoom);
        Assert.Equal(student.AdmissionNumber, updated.AdmissionNumber);
    }

    // ---------- E3.4 search ----------

    [Fact]
    public async Task Search_matches_every_word_against_names_and_admission_number()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var room = await CreateClassAsync(admin);
        var surname = "Zz" + Guid.NewGuid().ToString("N")[..8];
        var ada = await CreateStudentAsync(admin, room.Id, first: "Adaeze", last: surname);
        await CreateStudentAsync(admin, room.Id, first: "Bola", last: surname);

        var teacher = await Api.ClientAsync(Roles.Teacher); // teachers can search
        var byTwoWords = (await teacher.GetFromJsonAsync<PagedResult<StudentListItem>>($"/api/students?search=ada {surname.ToLowerInvariant()}", Json.Options))!;
        var byNumber = (await teacher.GetFromJsonAsync<PagedResult<StudentListItem>>($"/api/students?search={Uri.EscapeDataString(ada.AdmissionNumber)}", Json.Options))!;
        var byClass = (await teacher.GetFromJsonAsync<PagedResult<StudentListItem>>($"/api/students?classRoomId={room.Id}", Json.Options))!;

        Assert.Equal(ada.Id, Assert.Single(byTwoWords.Items).Id);
        Assert.Equal(ada.Id, Assert.Single(byNumber.Items).Id);
        Assert.Equal(2, byClass.TotalCount);
    }

    // ---------- E3.2 guardians ----------

    [Fact]
    public async Task Siblings_share_one_guardian_found_by_phone_in_any_format()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var room = await CreateClassAsync(admin);
        var older = await CreateStudentAsync(admin, room.Id);
        var younger = await CreateStudentAsync(admin, room.Id);
        var local = UniqueLocalPhone();

        var first = await AddGuardianAsync(admin, older.Id, local, email: null);
        var second = await AddGuardianAsync(admin, younger.Id, "+234 " + local[1..], email: null, name: "Different Spelling");

        Assert.False(first.ReusedExistingGuardian);
        Assert.True(second.ReusedExistingGuardian);
        Assert.Equal(first.GuardianId, second.GuardianId);
        Assert.Equal(first.Student.Guardians.Single().FullName, second.Student.Guardians.Single().FullName); // existing details kept
        Assert.True(second.Student.Guardians.Single().IsPrimaryContact); // first guardian becomes primary

        var guardian = (await admin.GetFromJsonAsync<GuardianDetail>($"/api/guardians/{first.GuardianId}", Json.Options))!;
        Assert.Equal(["+234" + local[1..]], new[] { guardian.Phone });
        Assert.Equal(2, guardian.Children.Count);

        await (await admin.PostAsJsonAsync($"/api/students/{older.Id}/guardians", GuardianBody(local))).AssertProblemAsync(HttpStatusCode.Conflict, "guardian.already_linked");
    }

    [Fact]
    public async Task Only_one_primary_contact_and_removing_it_promotes_another()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin, (await CreateClassAsync(admin)).Id);
        var mother = await AddGuardianAsync(admin, student.Id, UniqueLocalPhone(), relationship: "Mother");
        var father = await AddGuardianAsync(admin, student.Id, UniqueLocalPhone(), relationship: "Father", primary: true);

        Assert.Equal(father.GuardianId, Assert.Single(father.Student.Guardians, g => g.IsPrimaryContact).GuardianId);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/students/{student.Id}/guardians/{father.GuardianId}")).StatusCode);
        var after = (await admin.GetFromJsonAsync<StudentDetail>($"/api/students/{student.Id}", Json.Options))!;
        Assert.Equal(mother.GuardianId, Assert.Single(after.Guardians).GuardianId);
        Assert.True(after.Guardians.Single().IsPrimaryContact);
    }

    [Fact]
    public async Task Guardian_phone_must_be_valid_and_unique()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin, (await CreateClassAsync(admin)).Id);
        var a = await AddGuardianAsync(admin, student.Id, UniqueLocalPhone());
        var otherPhone = UniqueLocalPhone();
        await AddGuardianAsync(admin, student.Id, otherPhone);

        var invalid = await admin.PostAsJsonAsync($"/api/students/{student.Id}/guardians", GuardianBody("call me"));
        Assert.True((await invalid.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors").TryGetProperty("phone", out _));

        var clash = await admin.PutAsJsonAsync($"/api/guardians/{a.GuardianId}", new { fullName = "A", phone = otherPhone });
        await clash.AssertProblemAsync(HttpStatusCode.Conflict, "guardian.phone_in_use");
    }

    // ---------- E3.3 logins ----------

    [Fact]
    public async Task Student_login_uses_the_admission_number_and_a_reissue_revokes_the_old_one()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin, (await CreateClassAsync(admin)).Id);

        var first = (await (await admin.PostAsync($"/api/students/{student.Id}/login", null)).Content.ReadFromJsonAsync<IssuedCredentials>(Json.Options))!;
        Assert.Equal(student.AdmissionNumber, first.UserName);
        var session = await Api.LoginAsync(first.UserName, first.TemporaryPassword);
        Assert.True(session.User.MustChangePassword);
        Assert.Equal([Role.Student], session.User.Roles);

        var second = (await (await admin.PostAsync($"/api/students/{student.Id}/login", null)).Content.ReadFromJsonAsync<IssuedCredentials>(Json.Options))!;
        Assert.NotEqual(first.TemporaryPassword, second.TemporaryPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient(session.AccessToken).GetAsync("/api/auth/me")).StatusCode);
        await Api.LoginAsync(second.UserName, second.TemporaryPassword);

        // Never retrievable afterwards.
        Assert.DoesNotContain(second.TemporaryPassword, await admin.GetStringAsync($"/api/students/{student.Id}"));
    }

    [Fact]
    public async Task Parent_login_uses_email_or_else_phone_and_covers_all_children()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var room = await CreateClassAsync(admin);
        var a = await CreateStudentAsync(admin, room.Id);
        var b = await CreateStudentAsync(admin, room.Id);
        var phone = UniqueLocalPhone();
        var guardian = await AddGuardianAsync(admin, a.Id, phone, email: null);
        await AddGuardianAsync(admin, b.Id, phone, email: null);

        var issued = (await (await admin.PostAsync($"/api/guardians/{guardian.GuardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!;

        Assert.Equal("+234" + phone[1..], issued.Credentials!.UserName);
        var me = await Api.LoginAsync(issued.Credentials.UserName, issued.Credentials.TemporaryPassword);
        Assert.Equal([Role.Parent], me.User.Roles);

        var withEmail = await AddGuardianAsync(admin, a.Id, UniqueLocalPhone(), email: ApiFactory.UniqueEmail());
        var emailLogin = (await (await admin.PostAsync($"/api/guardians/{withEmail.GuardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!;
        Assert.Contains("@", emailLogin.Credentials!.UserName);
    }

    [Fact]
    public async Task A_teacher_who_is_also_a_parent_gets_the_parent_role_on_their_existing_account()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Teacher, email);
        var student = await CreateStudentAsync(admin, (await CreateClassAsync(admin)).Id);
        var guardian = await AddGuardianAsync(admin, student.Id, UniqueLocalPhone(), email: email);

        var issued = (await (await admin.PostAsync($"/api/guardians/{guardian.GuardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!;

        Assert.True(issued.LinkedExistingAccount);
        Assert.Null(issued.Credentials);
        Assert.Equal([Role.Teacher, Role.Parent], (await Api.LoginAsync(email)).User.Roles);
    }

    // ---------- E3.5 lifecycle ----------

    [Fact]
    public async Task Withdrawing_a_student_cuts_off_their_login_and_readmission_restores_it()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin, (await CreateClassAsync(admin)).Id);
        var credentials = (await (await admin.PostAsync($"/api/students/{student.Id}/login", null)).Content.ReadFromJsonAsync<IssuedCredentials>(Json.Options))!;
        var session = await Api.LoginAsync(credentials.UserName, credentials.TemporaryPassword);

        var withdrawn = await admin.PostAsJsonAsync($"/api/students/{student.Id}/status", new { status = "Withdrawn" });
        Assert.Equal(StudentStatus.Withdrawn, (await withdrawn.Content.ReadFromJsonAsync<StudentDetail>(Json.Options))!.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient(session.AccessToken).GetAsync("/api/auth/me")).StatusCode);
        await (await Api.CreateClient().PostAsJsonAsync("/api/auth/login", new { userName = credentials.UserName, password = credentials.TemporaryPassword }))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        await (await admin.PostAsync($"/api/students/{student.Id}/login", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "student.inactive");

        (await admin.PostAsJsonAsync($"/api/students/{student.Id}/status", new { status = "Active" })).EnsureSuccessStatusCode();
        await Api.LoginAsync(credentials.UserName, credentials.TemporaryPassword);
    }

    [Fact]
    public async Task Graduated_is_final()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin, (await CreateClassAsync(admin)).Id);
        (await admin.PostAsJsonAsync($"/api/students/{student.Id}/status", new { status = "Graduated" })).EnsureSuccessStatusCode();

        await (await admin.PostAsJsonAsync($"/api/students/{student.Id}/status", new { status = "Active" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "student.status_transition");
    }

    [Fact]
    public async Task Readmission_respects_class_capacity()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var room = await CreateClassAsync(admin, capacity: 1);
        var leaver = await CreateStudentAsync(admin, room.Id);
        (await admin.PostAsJsonAsync($"/api/students/{leaver.Id}/status", new { status = "Withdrawn" })).EnsureSuccessStatusCode();
        await CreateStudentAsync(admin, room.Id); // takes the freed place

        await (await admin.PostAsJsonAsync($"/api/students/{leaver.Id}/status", new { status = "Active" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "classroom.full");
    }

    // ---------- Authorization (role + row) ----------

    [Theory]
    [InlineData(Roles.Teacher)]
    [InlineData(Roles.Bursar)]
    public async Task Non_admin_staff_can_read_but_not_change_students(string role)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var student = await CreateStudentAsync(admin, (await CreateClassAsync(admin)).Id);
        var client = await Api.ClientAsync(role);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/students/{student.Id}")).StatusCode);
        await (await client.PostAsJsonAsync("/api/students", StudentBody(null))).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PutAsJsonAsync($"/api/students/{student.Id}", StudentBody(null))).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PostAsJsonAsync($"/api/students/{student.Id}/status", new { status = "Suspended" })).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PostAsync($"/api/students/{student.Id}/login", null)).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.PostAsJsonAsync($"/api/students/{student.Id}/guardians", GuardianBody(UniqueLocalPhone()))).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await client.GetAsync("/api/guardians")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_parent_cannot_read_any_student_through_the_staff_api_not_even_their_own_child()
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var room = await CreateClassAsync(admin);
        var own = await CreateStudentAsync(admin, room.Id);
        var other = await CreateStudentAsync(admin, room.Id);
        var guardian = await AddGuardianAsync(admin, own.Id, UniqueLocalPhone(), email: null);
        var credentials = (await (await admin.PostAsync($"/api/guardians/{guardian.GuardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!.Credentials!;
        var parent = Api.CreateClient((await ChangedPasswordSessionAsync(credentials)).AccessToken);

        // The parent portal (M1.3) is the parent's way in; the staff API is closed to them.
        await (await parent.GetAsync($"/api/students/{other.Id}")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await parent.GetAsync($"/api/students/{own.Id}")).AssertProblemAsync(HttpStatusCode.Forbidden);
        await (await parent.GetAsync("/api/students")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Students_cannot_use_the_staff_api()
    {
        var student = await Api.ClientAsync(Roles.Student);
        await (await student.GetAsync("/api/students")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    private static int _phoneSeq = Random.Shared.Next(1_000_000, 8_000_000);

    /// <summary>A fresh Nigerian local-format number, e.g. 08031234567.</summary>
    private static string UniqueLocalPhone() => $"0809{Interlocked.Increment(ref _phoneSeq):0000000}";

    private static object StudentBody(Guid? classRoomId, string first = "Ada", string last = "Okeke", string? dob = "2018-03-10",
        string admittedOn = "2026-09-14", string? house = null) => new
    {
        firstName = first, lastName = last, dateOfBirth = dob, sex = "Female", stateOfOrigin = "Anambra",
        house, classRoomId, admittedOn,
    };

    private static object GuardianBody(string phone, string? email = null, string name = "Mrs. Ngozi Okeke", string relationship = "Mother", bool primary = false) =>
        new { fullName = name, phone, email, relationship, isPrimaryContact = primary, canPickUp = true };

    private static async Task<ClassRoomDto> CreateClassAsync(HttpClient admin, int capacity = 30)
    {
        var level = (await admin.GetFromJsonAsync<List<ClassLevelDto>>("/api/academics/class-levels", Json.Options))!.First();
        var response = await admin.PostAsJsonAsync("/api/academics/classrooms", new { classLevelId = level.Id, arm = "S" + Guid.NewGuid().ToString("N")[..8], capacity });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClassRoomDto>(Json.Options))!;
    }

    private static async Task<StudentDetail> CreateStudentAsync(HttpClient admin, Guid? classRoomId, string first = "Ada", string last = "Okeke",
        string admittedOn = "2026-09-14", string dob = "2018-03-10")
    {
        var response = await admin.PostAsJsonAsync("/api/students", StudentBody(classRoomId, first, last, dob, admittedOn));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StudentDetail>(Json.Options))!;
    }

    private static async Task<GuardianAdded> AddGuardianAsync(HttpClient admin, Guid studentId, string phone, string? email = null,
        string name = "Mrs. Ngozi Okeke", string relationship = "Mother", bool primary = false)
    {
        var response = await admin.PostAsJsonAsync($"/api/students/{studentId}/guardians", GuardianBody(phone, email, name, relationship, primary));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GuardianAdded>(Json.Options))!;
    }

    private async Task<AuthResponse> ChangedPasswordSessionAsync(IssuedCredentials credentials)
    {
        var first = await Api.LoginAsync(credentials.UserName, credentials.TemporaryPassword);
        var change = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = credentials.TemporaryPassword, newPassword = NewPassword });
        change.EnsureSuccessStatusCode();
        return (await change.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
    }
}
