using System.Text.Json;

namespace Maydel.Application.Audit;

/// <summary>
/// One recorded data change. <see cref="Changes"/> is an object keyed by property name: for creates and deletes the value,
/// for updates <c>{ "from": …, "to": … }</c>. Secrets appear as <c>"***"</c>.
/// </summary>
public sealed record AuditLogEntry(
    long Id,
    DateTimeOffset Timestamp,
    Guid? UserId,
    string? UserName,
    string Action,
    string EntityType,
    string? EntityId,
    string? IpAddress,
    JsonElement? Changes);

/// <summary>Values for the viewer's filter dropdowns.</summary>
public sealed record AuditFilters(IReadOnlyList<string> EntityTypes, IReadOnlyList<string> Actions);
