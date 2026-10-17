using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maydel.Application.Abstractions;
using Maydel.Application.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Maydel.Api.Tests.Infrastructure;

/// <summary>Owns one PostgreSQL container for the whole test run; every API factory points at it.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public ApiFactory Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Api = new ApiFactory(ConnectionString);
        _ = Api.Server; // boots the app: migrates and seeds roles
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<PostgresFixture>;

/// <summary>The real app against a real database, with Google verification faked and rate limits relaxed (unless overridden).</summary>
public class ApiFactory(string connectionString, IDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    public const string TestPassword = "Test#Pass2026";

    /// <summary>Local file storage for this test run (photos etc.).</summary>
    public static readonly string StorageRoot = Path.Combine(Path.GetTempPath(), "maydel-tests-" + Guid.NewGuid().ToString("N")[..8]);
    public FakeGoogleVerifier Google { get; } = new();
    public FakeEmailSender Email { get; } = new();
    public FakeSchoolClock Clock { get; } = new();
    public FakePaymentGateway Paystack { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var all = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Jwt:SigningKey"] = "integration-tests-signing-key-0123456789abcdef",
            ["Database:MigrateOnStartup"] = "true",
            ["RateLimiting:AuthPermitLimit"] = "100000",
            ["RateLimiting:PublicFormsPermitLimit"] = "100000",
            ["Storage:RootPath"] = StorageRoot,
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>()) all[key] = value;
        foreach (var (key, value) in all) builder.UseSetting(key, value);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGoogleIdTokenVerifier>();
            services.AddSingleton<IGoogleIdTokenVerifier>(Google);
            // No background sending in tests: they call EmailDispatcher directly against this fake.
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(Paystack);
            services.RemoveAll<ISchoolClock>();
            services.AddSingleton<ISchoolClock>(Clock);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
            foreach (var worker in services.Where(d => d.ImplementationType?.Name == "EmailOutboxWorker").ToList()) services.Remove(worker);
        });
    }

    public async Task<AppUser> CreateUserAsync(string role, string? email = null, bool mustChangePassword = false, string? userName = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var user = new AppUser
        {
            UserName = userName ?? email ?? $"user-{tag}",
            Email = email,
            EmailConfirmed = email is not null,
            FullName = $"{role} {tag}",
            MustChangePassword = mustChangePassword,
        };
        var created = await users.CreateAsync(user, TestPassword);
        if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
        return user;
    }

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@maydel.test";

    public async Task<AuthResponse> LoginAsync(string userName, string password = TestPassword)
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/login", new { userName, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
    }

    public HttpClient CreateClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>A fresh user in <paramref name="role"/>, signed in; returns a client carrying their bearer token.</summary>
    public async Task<HttpClient> ClientAsync(string role)
    {
        var email = UniqueEmail();
        await CreateUserAsync(role, email);
        return CreateClient((await LoginAsync(email)).AccessToken);
    }

    public async Task WithServicesAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }
}

public sealed class FakeGoogleVerifier : IGoogleIdTokenVerifier
{
    private readonly Dictionary<string, GoogleIdentity> _tokens = [];

    public bool IsEnabled { get; set; } = true;
    public const string ExplodingToken = "explode";

    /// <summary>Registers a token the fake will accept, as if Google had signed it.</summary>
    public string Issue(string email, string? subject = null, bool emailVerified = true)
    {
        var token = Guid.NewGuid().ToString("N");
        lock (_tokens) _tokens[token] = new GoogleIdentity(subject ?? Guid.NewGuid().ToString("N"), email, emailVerified);
        return token;
    }

    public Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct)
    {
        if (idToken == ExplodingToken) throw new InvalidOperationException("Simulated failure with secret detail: db-password=hunter2");
        lock (_tokens) return Task.FromResult(_tokens.GetValueOrDefault(idToken));
    }
}

public static class Json
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
