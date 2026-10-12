using System.Text.Json;
using Maydel.Application.Abstractions;
using Maydel.Domain.Audit;
using Maydel.Domain.Common;
using Maydel.Application.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Maydel.Infrastructure.Persistence;

/// <summary>
/// Stamps CreatedAt/UpdatedAt and appends an <see cref="AuditLog"/> row for every
/// insert, update and delete of a domain entity, inside the same transaction.
/// </summary>
public sealed class AuditInterceptor(ICurrentUser currentUser, TimeProvider clock) : SaveChangesInterceptor
{
    // Never write secrets into the audit trail.
    private static readonly HashSet<string> RedactedProperties =
        [nameof(AppUser.PasswordHash), nameof(AppUser.SecurityStamp), nameof(AppUser.ConcurrencyStamp), nameof(RefreshToken.TokenHash)];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext context)
    {
        var now = clock.GetUtcNow();
        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is Entity entity)
            {
                if (entry.State == EntityState.Added) entity.CreatedAt = now;
                else if (entry.State == EntityState.Modified) entity.UpdatedAt = now;
            }

            if (entry.Entity is AuditLog or RefreshToken) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            // Owned rows (score components, invoice lines) surface through their owner.
            if (entry.Metadata.IsOwned()) continue;

            var changes = Describe(entry);
            if (entry.State == EntityState.Modified && changes.Count == 0) continue;

            logs.Add(new AuditLog
            {
                Timestamp = now,
                UserId = currentUser.UserId,
                UserName = currentUser.UserName,
                IpAddress = currentUser.IpAddress,
                Action = entry.State.ToString(),
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = string.Join(",", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue)),
                Changes = JsonSerializer.Serialize(changes),
            });
        }

        if (logs.Count > 0) context.Set<AuditLog>().AddRange(logs);
    }

    private static Dictionary<string, object?> Describe(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();
        foreach (var p in entry.Properties)
        {
            var name = p.Metadata.Name;
            if (name is nameof(Entity.CreatedAt) or nameof(Entity.UpdatedAt)) continue;
            var redact = RedactedProperties.Contains(name);

            switch (entry.State)
            {
                case EntityState.Added:
                    changes[name] = redact ? "***" : p.CurrentValue;
                    break;
                case EntityState.Deleted:
                    changes[name] = redact ? "***" : p.OriginalValue;
                    break;
                case EntityState.Modified when p.IsModified && !Equals(p.OriginalValue, p.CurrentValue):
                    changes[name] = redact ? "***" : new { from = p.OriginalValue, to = p.CurrentValue };
                    break;
            }
        }
        return changes;
    }
}
