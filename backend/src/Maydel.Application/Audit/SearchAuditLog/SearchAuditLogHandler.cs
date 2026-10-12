using System.Text.Json;
using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Audit.SearchAuditLog;

internal sealed class SearchAuditLogHandler(IAppDbContext db) : IQueryHandler<SearchAuditLogQuery, PagedResult<AuditLogEntry>>
{
    public async Task<Result<PagedResult<AuditLogEntry>>> Handle(SearchAuditLogQuery q, CancellationToken ct)
    {
        var logs = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.EntityType)) logs = logs.Where(l => l.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) logs = logs.Where(l => l.EntityId == q.EntityId);
        if (q.UserId is { } userId) logs = logs.Where(l => l.UserId == userId);
        if (!string.IsNullOrWhiteSpace(q.Action)) logs = logs.Where(l => l.Action.ToLower() == q.Action.ToLower());
        if (q.From is { } from) logs = logs.Where(l => l.Timestamp >= from);
        if (q.To is { } to) logs = logs.Where(l => l.Timestamp < to);

        var page = await logs
            .OrderByDescending(l => l.Id)
            .Select(l => new { l.Id, l.Timestamp, l.UserId, l.UserName, l.Action, l.EntityType, l.EntityId, l.IpAddress, l.Changes })
            .ToPagedAsync(q.Page, q.PageSize, ct);

        return new PagedResult<AuditLogEntry>(
            [.. page.Items.Select(l => new AuditLogEntry(
                l.Id, l.Timestamp, l.UserId, l.UserName, l.Action, l.EntityType, l.EntityId, l.IpAddress, Parse(l.Changes)))],
            page.Page, page.PageSize, page.TotalCount);
    }

    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
