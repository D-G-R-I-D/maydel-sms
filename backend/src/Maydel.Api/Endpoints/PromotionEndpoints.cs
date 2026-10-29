using Maydel.Api.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Promotions;
using Maydel.Application.Promotions.GetPromotionPlan;
using Maydel.Application.Promotions.PromoteStudents;
using Maydel.Application.Promotions.UndoPromotion;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>End-of-session promotion (E3.6): preview, apply, undo. Admins only.</summary>
public static class PromotionEndpoints
{
    public static void MapPromotionEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/promotions").WithTags("Promotions").RequireAuthorization(Policies.Admin);
        admin.MapGet("", GetPlan).WithName("GetPromotionPlan").ProducesProblems(401, 403, 404);
        admin.MapPost("", Promote).WithName("PromoteStudents").ProducesProblems(400, 401, 403, 404, 409, 422);
        admin.MapPost("/undo", Undo).WithName("UndoPromotion").ProducesProblems(400, 401, 403, 404, 422);
    }

    private static async Task<Results<Ok<PromotionPlan>, ProblemHttpResult>> GetPlan(
        Guid? sessionId, Guid? classRoomId, IQueryHandler<GetPromotionPlanQuery, PromotionPlan> handler, CancellationToken ct) =>
        (await handler.Handle(new GetPromotionPlanQuery(sessionId, classRoomId), ct)).ToHttpResult();

    private static async Task<Results<Ok<PromotionSummary>, ProblemHttpResult>> Promote(
        PromoteStudentsCommand command, ICommandHandler<PromoteStudentsCommand, PromotionSummary> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<PromotionUndone>, ProblemHttpResult>> Undo(
        UndoPromotionCommand command, ICommandHandler<UndoPromotionCommand, PromotionUndone> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();
}
