using System.ComponentModel.DataAnnotations;
using Maydel.Api.Auth;
using Maydel.Api.Common;
using Maydel.Domain.Common;
using Maydel.Domain.People;
using Maydel.Infrastructure.Identity;
using Maydel.Infrastructure.Persistence;
using Maydel.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Api.Features;

public sealed record StaffDto(
    Guid Id, string StaffNumber, string FullName, string Email, string? Phone, string? JobTitle, string? Bio,
    bool ShowOnWebsite, int WebsiteOrder, bool IsActive, string? Role, string? PhotoUrl);

public sealed record UpsertStaffRequest(
    [property: Required, MaxLength(150)] string FullName,
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Phone, MaxLength(32)] string? Phone,
    [property: MaxLength(100)] string? JobTitle,
    [property: MaxLength(2000)] string? Bio,
    [property: Required] string Role,
    bool ShowOnWebsite,
    int WebsiteOrder,
    bool IsActive = true);

public static class StaffEndpoints
{
    public static void MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/staff").WithTags("Staff").RequireAuthorization(Policies.Admin);

        g.MapGet("", async (AppDbContext db, CancellationToken ct) =>
        {
            var roles = await RoleLookupAsync(db, ct);
            var staff = await db.Staff.AsNoTracking().OrderBy(s => s.FullName).ToListAsync(ct);
            return staff.Select(s => ToDto(s, roles));
        });

        g.MapPost("", async (UpsertStaffRequest req, AppDbContext db, UserManager<AppUser> users, CancellationToken ct) =>
        {
            if (!Roles.Staff.Contains(req.Role)) return Problems.Validation("role", "Role must be Admin, Teacher or Bursar.");
            var email = req.Email.Trim().ToLowerInvariant();
            if (await users.FindByEmailAsync(email) is not null) return Problems.Conflict("An account with this email already exists.");

            var password = StudentEndpoints.TemporaryPassword();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = req.FullName.Trim(), MustChangePassword = true };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded) return Problems.Validation(created);
            await users.AddToRoleAsync(user, req.Role);

            var count = await db.Staff.CountAsync(ct);
            var staff = new Staff { StaffNumber = $"MSS-STF-{count + 1:000}", FullName = req.FullName.Trim(), Email = email, UserId = user.Id };
            Apply(staff, req);
            db.Staff.Add(staff);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { Staff = ToDto(staff, new() { [user.Id] = req.Role }), Credentials = new CredentialsResponse(email, password) });
        });

        g.MapPut("/{id:guid}", async (Guid id, UpsertStaffRequest req, AppDbContext db, UserManager<AppUser> users, CurrentUser me, CancellationToken ct) =>
        {
            if (!Roles.Staff.Contains(req.Role)) return Problems.Validation("role", "Role must be Admin, Teacher or Bursar.");
            var staff = await db.Staff.FindAsync([id], ct);
            if (staff is null) return Problems.NotFound();
            if (staff.UserId == me.UserId && (!req.IsActive || req.Role != Roles.Admin))
                return Problems.Rule("You cannot deactivate or demote your own account.");

            Apply(staff, req);
            staff.FullName = req.FullName.Trim();
            await db.SaveChangesAsync(ct);

            if (staff.UserId is { } uid && await users.FindByIdAsync(uid.ToString()) is { } user)
            {
                var current = await users.GetRolesAsync(user);
                var changed = !current.SequenceEqual([req.Role]) || user.IsActive != req.IsActive;
                await users.RemoveFromRolesAsync(user, current);
                await users.AddToRoleAsync(user, req.Role);
                user.IsActive = req.IsActive;
                user.FullName = staff.FullName;
                await users.UpdateAsync(user);
                // Role or status change invalidates existing access tokens immediately.
                if (changed) await users.UpdateSecurityStampAsync(user);
            }
            return Results.Ok(ToDto(staff, await RoleLookupAsync(db, ct)));
        });

        g.MapPost("/{id:guid}/credentials", async (Guid id, AppDbContext db, UserManager<AppUser> users, TokenService tokens, CancellationToken ct) =>
        {
            var staff = await db.Staff.FindAsync([id], ct);
            if (staff?.UserId is not { } uid || await users.FindByIdAsync(uid.ToString()) is not { } user) return Problems.NotFound();
            var password = StudentEndpoints.TemporaryPassword();
            await users.RemovePasswordAsync(user);
            var result = await users.AddPasswordAsync(user, password);
            if (!result.Succeeded) return Problems.Validation(result);
            user.MustChangePassword = true;
            await users.SetLockoutEndDateAsync(user, null);
            await users.UpdateAsync(user);
            await users.UpdateSecurityStampAsync(user);
            await tokens.RevokeAllForUserAsync(user.Id);
            return Results.Ok(new CredentialsResponse(user.UserName!, password));
        });

        g.MapPost("/{id:guid}/photo", async (Guid id, IFormFile file, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var staff = await db.Staff.FindAsync([id], ct);
            if (staff is null) return Problems.NotFound();
            var (ext, error) = await Files.ValidateImageAsync(file);
            if (error is not null) return Problems.Validation("file", error);
            await using var stream = file.OpenReadStream();
            var old = staff.PhotoPath;
            staff.PhotoPath = await storage.SaveAsync(stream, "staff", ext!, ct);
            await db.SaveChangesAsync(ct);
            if (old is not null) await storage.DeleteAsync(old, ct);
            return Results.NoContent();
        }).DisableAntiforgery();

        // Lightweight list for teacher pickers, available to all staff.
        app.MapGet("/api/staff/teachers", async (AppDbContext db, CancellationToken ct) =>
            await db.Staff.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.FullName)
                .Select(s => new { s.Id, s.FullName, s.JobTitle }).ToListAsync(ct))
            .RequireAuthorization(Policies.Staff).WithTags("Staff");
    }

    private static void Apply(Staff s, UpsertStaffRequest r)
    {
        s.Phone = r.Phone;
        s.JobTitle = r.JobTitle;
        s.Bio = r.Bio;
        s.ShowOnWebsite = r.ShowOnWebsite;
        s.WebsiteOrder = r.WebsiteOrder;
        s.IsActive = r.IsActive;
    }

    private static async Task<Dictionary<Guid, string>> RoleLookupAsync(AppDbContext db, CancellationToken ct) =>
        await (from ur in db.UserRoles
               join r in db.Roles on ur.RoleId equals r.Id
               where r.Name == Roles.Admin || r.Name == Roles.Teacher || r.Name == Roles.Bursar
               select new { ur.UserId, r.Name })
            .ToDictionaryAsync(x => x.UserId, x => x.Name!, ct);

    private static StaffDto ToDto(Staff s, Dictionary<Guid, string> roles) =>
        new(s.Id, s.StaffNumber, s.FullName, s.Email, s.Phone, s.JobTitle, s.Bio, s.ShowOnWebsite, s.WebsiteOrder, s.IsActive,
            s.UserId is { } uid && roles.TryGetValue(uid, out var role) ? role : null, Files.Url(s.PhotoPath));
}
