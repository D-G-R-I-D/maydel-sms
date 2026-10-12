using Maydel.Application.Abstractions;
using Maydel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Maydel.Application.Tests.Infrastructure;

/// <summary>One real PostgreSQL per test run, migrated once. Tests isolate themselves with unique data, not by resetting.</summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext(new TestUser());
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    public AppDbContext CreateContext(ICurrentUser user) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new AuditInterceptor(user, TimeProvider.System))
            .Options);
}

[CollectionDefinition(nameof(DatabaseCollection))]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;

public sealed class TestUser(Guid? userId = null, params string[] roles) : ICurrentUser
{
    public Guid? UserId { get; } = userId;
    public string? UserName => UserId?.ToString();
    public bool IsInRole(string role) => roles.Contains(role);
    public string? IpAddress => "127.0.0.1";
    public string? UserAgent => "tests";
}
