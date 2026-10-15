using Maydel.Application.Abstractions;
using Maydel.Application.Identity;
using Maydel.Infrastructure.Identity;
using Maydel.Infrastructure.Persistence;
using Maydel.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maydel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISchoolClock, Time.SchoolClock>();
        services.AddScoped<AuditInterceptor>();

        // Resolved lazily so tests and tooling can supply configuration after registration.
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' is not configured (ConnectionStrings__Default)."))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services
            .AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 10;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireDigit = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.Lockout.AllowedForNewUsers = true;
                o.User.RequireUniqueEmail = false; // students have no email; staff/parent uniqueness is enforced when accounts are created
                o.User.AllowedUserNameCharacters += "/";
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.Section)
            .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey must be at least 32 characters (set Jwt__SigningKey).")
            .Validate(o => o.AccessTokenMinutes is > 0 and <= 60 && o.RefreshTokenDays is > 0 and <= 90, "Jwt token lifetimes are out of range.")
            .ValidateOnStart();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddOptions<GoogleAuthOptions>().BindConfiguration(GoogleAuthOptions.Section);
        services.AddSingleton<IGoogleIdTokenVerifier, GoogleIdTokenVerifier>();

        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.Section);
        services.AddScoped<DbSeeder>();

        services.AddOptions<StorageOptions>().BindConfiguration(StorageOptions.Section)
            .Validate(o => o.Provider is "Local" or "S3", "Storage:Provider must be Local or S3.")
            .ValidateOnStart();
        services.AddSingleton<IFileStorage>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>().Value.Provider == "S3"
                ? ActivatorUtilities.CreateInstance<S3FileStorage>(sp)
                : ActivatorUtilities.CreateInstance<LocalFileStorage>(sp));
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();

        services.AddOptions<SchoolProfileOptions>().BindConfiguration(SchoolProfileOptions.Section);
        services.AddSingleton<IReportCardRenderer, Reports.QuestPdfReportCardRenderer>();
        services.AddSingleton<IFinanceDocumentRenderer, Reports.QuestPdfFinanceRenderer>();

        // E12.3 email outbox. The provider is configuration; secrets come from the environment.
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.Section)
            .Validate(o => o.Provider is "None" or "Pickup" or "Smtp" or "Resend", "Email:Provider must be None, Pickup, Smtp or Resend.")
            .Validate(o => o.Provider != "Smtp" || !string.IsNullOrWhiteSpace(o.SmtpHost), "Email:SmtpHost is required for the Smtp provider.")
            .Validate(o => o.Provider != "Resend" || !string.IsNullOrWhiteSpace(o.ResendApiKey), "Email:ResendApiKey is required for the Resend provider.")
            .ValidateOnStart();
        services.AddHttpClient<Notifications.ResendEmailSender>(c => c.BaseAddress = new Uri("https://api.resend.com/"));
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value.Provider switch
        {
            "Pickup" => ActivatorUtilities.CreateInstance<Notifications.PickupEmailSender>(sp),
            "Smtp" => ActivatorUtilities.CreateInstance<Notifications.SmtpEmailSender>(sp),
            "Resend" => sp.GetRequiredService<Notifications.ResendEmailSender>(),
            _ => new Notifications.DisabledEmailSender(),
        });
        services.AddHostedService<Notifications.EmailOutboxWorker>();

        return services;
    }
}
