using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Audit.SearchAuditLog;

/// <summary>Newest first. All filters are optional and combine with AND. <c>to</c> is exclusive.</summary>
public sealed record SearchAuditLogQuery(
    string? EntityType = null,
    string? EntityId = null,
    Guid? UserId = null,
    AuditAction? Action = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int? Page = null,
    int? PageSize = null) : IQuery<PagedResult<AuditLogEntry>>;
