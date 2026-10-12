using Maydel.Api.Common;
using Maydel.Application.Audit;
using Maydel.Application.Audit.GetAuditFilters;
using Maydel.Application.Audit.SearchAuditLog;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>Read-only audit viewer (E14.1). The log is append-only: there are deliberately no write endpoints.</summary>
public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit").WithTags("Audit").RequireAuthorization(Policies.Admin);

        group.MapGet("", Search).WithName("SearchAuditLog").ProducesProblems(400, 401, 403);
        group.MapGet("/filters", GetFilters).WithName("GetAuditFilters").ProducesProblems(401, 403);
    }

    private static async Task<Results<Ok<PagedResult<AuditLogEntry>>, ProblemHttpResult>> Search(
        [AsParameters] SearchAuditLogQuery query, IQueryHandler<SearchAuditLogQuery, PagedResult<AuditLogEntry>> handler, CancellationToken ct) =>
        (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<AuditFilters>, ProblemHttpResult>> GetFilters(
        IQueryHandler<GetAuditFiltersQuery, AuditFilters> handler, CancellationToken ct) =>
        (await handler.Handle(new GetAuditFiltersQuery(), ct)).ToHttpResult();
}
