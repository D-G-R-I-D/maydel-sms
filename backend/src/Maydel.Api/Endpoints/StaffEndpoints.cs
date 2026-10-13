using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Application.StaffMembers;
using Maydel.Application.StaffMembers.CreateStaff;
using Maydel.Application.StaffMembers.GetStaff;
using Maydel.Application.StaffMembers.ListStaff;
using Maydel.Application.StaffMembers.ResetStaffPassword;
using Maydel.Application.StaffMembers.StaffLookup;
using Maydel.Application.StaffMembers.UpdateStaff;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>Staff records and their logins (E4.1). Admin only, except the lookup used by teacher pickers.</summary>
public static class StaffEndpoints
{
    public static void MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/staff").WithTags("Staff").RequireAuthorization(Policies.Admin);

        admin.MapGet("", List).WithName("ListStaff").ProducesProblems(400, 401, 403);
        admin.MapGet("/{id:guid}", Get).WithName("GetStaff").ProducesProblems(401, 403, 404);
        admin.MapPost("", Create).WithName("CreateStaff").ProducesProblems(400, 401, 403, 409);
        admin.MapPut("/{id:guid}", Update).WithName("UpdateStaff").ProducesProblems(400, 401, 403, 404, 409, 422);
        admin.MapPost("/{id:guid}/reset-password", ResetPassword).WithName("ResetStaffPassword").ProducesProblems(401, 403, 404, 422);

        app.MapGet("/api/staff/lookup", Lookup).WithTags("Staff").RequireAuthorization(Policies.Staff)
            .WithName("StaffLookup").ProducesProblems(401, 403);
    }

    private static async Task<Results<Ok<PagedResult<StaffListItem>>, ProblemHttpResult>> List(
        [AsParameters] ListStaffQuery query, IQueryHandler<ListStaffQuery, PagedResult<StaffListItem>> handler, CancellationToken ct) =>
        (await handler.Handle(query, ct)).ToHttpResult();

    private static async Task<Results<Ok<StaffDetail>, ProblemHttpResult>> Get(
        Guid id, IQueryHandler<GetStaffQuery, StaffDetail> handler, CancellationToken ct) =>
        (await handler.Handle(new GetStaffQuery(id), ct)).ToHttpResult();

    private static async Task<Results<Created<StaffCreated>, ProblemHttpResult>> Create(
        CreateStaffCommand command, ICommandHandler<CreateStaffCommand, StaffCreated> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToCreatedResult(s => $"/api/staff/{s.Staff.Id}");

    private static async Task<Results<Ok<StaffDetail>, ProblemHttpResult>> Update(
        Guid id, StaffUpdateRequest body, ICommandHandler<UpdateStaffCommand, StaffDetail> handler, CancellationToken ct) =>
        (await handler.Handle(new UpdateStaffCommand(id, body.FullName, body.Email, body.Phone, body.JobTitle, body.Roles, body.IsActive), ct)).ToHttpResult();

    private static async Task<Results<Ok<IssuedCredentials>, ProblemHttpResult>> ResetPassword(
        Guid id, ICommandHandler<ResetStaffPasswordCommand, IssuedCredentials> handler, CancellationToken ct) =>
        (await handler.Handle(new ResetStaffPasswordCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<StaffLookupItem>>, ProblemHttpResult>> Lookup(
        IQueryHandler<StaffLookupQuery, IReadOnlyList<StaffLookupItem>> handler, CancellationToken ct) =>
        (await handler.Handle(new StaffLookupQuery(), ct)).ToHttpResult();
}
