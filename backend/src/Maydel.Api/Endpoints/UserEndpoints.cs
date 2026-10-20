using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Application.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>Settings → Users (Admin): every login, with enable/disable and password resets. Staff roles live on /api/staff.</summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization(Policies.Admin);
        admin.MapGet("", List).WithName("ListUsers").ProducesProblems(401, 403);
        admin.MapPost("/{id:guid}/disable", Disable).WithName("DisableUser").ProducesProblems(401, 403, 404, 422);
        admin.MapPost("/{id:guid}/enable", Enable).WithName("EnableUser").ProducesProblems(401, 403, 404, 422);
        admin.MapPost("/{id:guid}/reset-password", Reset).WithName("ResetUserPassword").ProducesProblems(401, 403, 404, 422);
    }

    private static async Task<Results<Ok<PagedResult<UserRow>>, ProblemHttpResult>> List([AsParameters] ListUsersQuery q, IQueryHandler<ListUsersQuery, PagedResult<UserRow>> h, CancellationToken ct) =>
        (await h.Handle(q, ct)).ToHttpResult();
    private static async Task<Results<Ok<UserRow>, ProblemHttpResult>> Disable(Guid id, ICommandHandler<SetUserActiveCommand, UserRow> h, CancellationToken ct) =>
        (await h.Handle(new SetUserActiveCommand(id, false), ct)).ToHttpResult();
    private static async Task<Results<Ok<UserRow>, ProblemHttpResult>> Enable(Guid id, ICommandHandler<SetUserActiveCommand, UserRow> h, CancellationToken ct) =>
        (await h.Handle(new SetUserActiveCommand(id, true), ct)).ToHttpResult();
    private static async Task<Results<Ok<IssuedCredentials>, ProblemHttpResult>> Reset(Guid id, ICommandHandler<ResetUserPasswordCommand, IssuedCredentials> h, CancellationToken ct) =>
        (await h.Handle(new ResetUserPasswordCommand(id), ct)).ToHttpResult();
}
