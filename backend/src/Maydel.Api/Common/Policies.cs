using Maydel.Domain.Common;
using Microsoft.AspNetCore.Authorization;

namespace Maydel.Api.Common;

/// <summary>Role policies (docs/04-security.md §3). Row-level rules live in <c>AccessRules</c>, not here.</summary>
public static class Policies
{
    public const string Admin = nameof(Admin);
    public const string Staff = nameof(Staff);
    public const string Academic = nameof(Academic);
    public const string Finance = nameof(Finance);
    public const string Portal = nameof(Portal);
    public const string Applicants = nameof(Applicants);
    public const string Students = nameof(Students);

    public static void Configure(AuthorizationOptions o)
    {
        o.AddPolicy(Admin, p => p.RequireRole(Roles.Admin));
        o.AddPolicy(Staff, p => p.RequireRole(Roles.Admin, Roles.Teacher, Roles.Bursar));
        o.AddPolicy(Academic, p => p.RequireRole(Roles.Admin, Roles.Teacher));
        o.AddPolicy(Finance, p => p.RequireRole(Roles.Admin, Roles.Bursar));
        o.AddPolicy(Portal, p => p.RequireRole(Roles.Parent, Roles.Student));
        // Families applying for admission; existing parents apply for siblings from the same account.
        o.AddPolicy(Applicants, p => p.RequireRole(Roles.Applicant, Roles.Parent));
        // Only students sit online tests; parents follow results through the portal.
        o.AddPolicy(Students, p => p.RequireRole(Roles.Student));

        // Secure by default: an endpoint that forgets to declare a policy still requires a signed-in user.
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }
}

public static class RateLimits
{
    public const string Auth = "auth";
    public const string PublicForms = "public-forms";
}

public sealed class RateLimitOptions
{
    public const string Section = "RateLimiting";

    /// <summary>Requests per minute per client IP on <c>/api/auth/*</c>.</summary>
    public int AuthPermitLimit { get; set; } = 10;

    /// <summary>Requests per minute per client IP on public forms (admissions, contact, newsletter).</summary>
    public int PublicFormsPermitLimit { get; set; } = 5;
}
