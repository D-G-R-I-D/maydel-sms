using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Audit;
using Maydel.Application.Common;
using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Maydel.Api.Tests.Audit;

/// <summary>E14.1 — automatic, redacted, append-only audit trail with an admin viewer.</summary>
[Collection(nameof(ApiCollection))]
public sealed class AuditTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;

    [Fact]
    public async Task Changes_record_who_and_what_with_secrets_redacted()
    {
        var email = ApiFactory.UniqueEmail();
        var user = await Api.CreateUserAsync(Roles.Teacher, email);
        var session = await Api.LoginAsync(email);
        (await Api.CreateClient(session.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = ApiFactory.TestPassword, newPassword = "Brand#NewPass2026" })).EnsureSuccessStatusCode();

        var page = await SearchAsync($"?entityType=AppUser&entityId={user.Id}&pageSize=50");

        var created = Assert.Single(page.Items, e => e.Action == "Added");
        Assert.Equal("***", created.Changes!.Value.GetProperty("PasswordHash").GetString());
        Assert.Equal(email, created.Changes.Value.GetProperty("Email").GetString());

        // The password change is attributed to the user who made it, and the hash never appears.
        var passwordChange = Assert.Single(page.Items, e => e.Action == "Modified" && e.Changes!.Value.TryGetProperty("PasswordHash", out _));
        Assert.Equal(user.Id, passwordChange.UserId);
        Assert.Equal(email, passwordChange.UserName);
        Assert.Equal("***", passwordChange.Changes!.Value.GetProperty("PasswordHash").GetString());
        Assert.DoesNotContain(page.Items, e => e.Changes?.GetRawText().Contains("AQAAAA") == true); // Identity v3 hash prefix
    }

    [Fact]
    public async Task Results_are_newest_first_and_paged()
    {
        await Api.CreateUserAsync(Roles.Parent, ApiFactory.UniqueEmail());
        await Api.CreateUserAsync(Roles.Parent, ApiFactory.UniqueEmail());

        var page = await SearchAsync("?pageSize=2");

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.PageSize);
        Assert.True(page.TotalCount >= 2);
        Assert.True(page.Items[0].Id > page.Items[1].Id);
    }

    [Fact]
    public async Task Invalid_filters_return_field_errors()
    {
        var client = await AdminClientAsync();

        var response = await client.GetAsync("/api/audit?pageSize=500&action=Exploded&from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z");

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation")).GetProperty("errors");
        Assert.True(errors.TryGetProperty("pageSize", out _));
        Assert.True(errors.TryGetProperty("action", out _));
        Assert.True(errors.TryGetProperty("to", out _));
    }

    [Fact]
    public async Task Filters_list_entity_types_seen_so_far()
    {
        await Api.CreateUserAsync(Roles.Parent, ApiFactory.UniqueEmail());
        var client = await AdminClientAsync();

        var filters = (await client.GetFromJsonAsync<AuditFilters>("/api/audit/filters", Json.Options))!;

        Assert.Contains("AppUser", filters.EntityTypes);
        Assert.Equal(["Added", "Modified", "Deleted"], filters.Actions);
    }

    [Fact]
    public async Task The_database_refuses_to_update_or_delete_audit_rows()
    {
        await Api.CreateUserAsync(Roles.Parent, ApiFactory.UniqueEmail());

        await Api.WithServicesAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var id = await db.AuditLogs.MaxAsync(l => l.Id);

            var delete = await Assert.ThrowsAsync<PostgresException>(() => db.AuditLogs.Where(l => l.Id == id).ExecuteDeleteAsync());
            Assert.Contains("append-only", delete.MessageText);
            var update = await Assert.ThrowsAsync<PostgresException>(() =>
                db.AuditLogs.Where(l => l.Id == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.UserName, "someone-else")));
            Assert.Contains("append-only", update.MessageText);
        });
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var email = ApiFactory.UniqueEmail();
        await Api.CreateUserAsync(Roles.Admin, email);
        return Api.CreateClient((await Api.LoginAsync(email)).AccessToken);
    }

    private async Task<PagedResult<AuditLogEntry>> SearchAsync(string query)
    {
        var response = await (await AdminClientAsync()).GetAsync("/api/audit" + query);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<AuditLogEntry>>(Json.Options))!;
    }
}
