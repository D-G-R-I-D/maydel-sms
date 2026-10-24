using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Newsletters;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>Newsletters (E12.6): compose, test, send or schedule (admin); archive, unsubscribe and confirm (public).</summary>
public static class NewsletterEndpoints
{
    public static void MapNewsletterEndpoints(this IEndpointRouteBuilder app)
    {
        var cms = app.MapGroup("/api/cms/newsletters").WithTags("Newsletters").RequireAuthorization(Policies.Admin);
        cms.MapGet("", List).WithName("ListNewsletters").ProducesProblems(401, 403);
        cms.MapGet("/{id:guid}", Get).WithName("GetNewsletter").ProducesProblems(401, 403, 404);
        cms.MapPost("", Create).WithName("CreateNewsletter").ProducesProblems(400, 401, 403);
        cms.MapPut("/{id:guid}", Update).WithName("UpdateNewsletter").ProducesProblems(400, 401, 403, 404, 422);
        cms.MapDelete("/{id:guid}", Delete).WithName("DeleteNewsletter").ProducesProblems(401, 403, 404, 422);
        cms.MapGet("/{id:guid}/recipients", Count).WithName("CountNewsletterRecipients").ProducesProblems(401, 403, 404);
        cms.MapPost("/{id:guid}/test", Test).WithName("TestNewsletter").ProducesProblems(400, 401, 403, 404);
        cms.MapPost("/{id:guid}/send", Send).WithName("SendNewsletter").ProducesProblems(400, 401, 403, 404, 422);
        cms.MapPost("/{id:guid}/cancel", Cancel).WithName("CancelNewsletterSchedule").ProducesProblems(401, 403, 404, 422);

        var site = app.MapGroup("/api/public").WithTags("Newsletters").AllowAnonymous();
        site.MapGet("/newsletters", Archive).WithName("ListPublicNewsletters");
        site.MapGet("/newsletters/{slug}", Issue).WithName("GetPublicNewsletter").ProducesProblems(404);
        // POST only: link scanners in mail apps follow GET links, which must never unsubscribe anyone.
        site.MapPost("/newsletter/unsubscribe", Unsubscribe).WithName("UnsubscribeFromNewsletter").RequireRateLimiting(RateLimits.PublicForms).ProducesProblems(404, 429);
        site.MapPost("/newsletter/confirm", Confirm).WithName("ConfirmNewsletterSubscription").RequireRateLimiting(RateLimits.PublicForms).ProducesProblems(404, 429);
    }

    private static async Task<Results<Ok<PagedResult<NewsletterSummary>>, ProblemHttpResult>> List([AsParameters] ListNewslettersQuery q, IQueryHandler<ListNewslettersQuery, PagedResult<NewsletterSummary>> h, CancellationToken ct) =>
        (await h.Handle(q, ct)).ToHttpResult();
    private static async Task<Results<Ok<NewsletterDto>, ProblemHttpResult>> Get(Guid id, IQueryHandler<GetNewsletterQuery, NewsletterDto> h, CancellationToken ct) =>
        (await h.Handle(new GetNewsletterQuery(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<NewsletterDto>, ProblemHttpResult>> Create(NewsletterInput body, ICommandHandler<SaveNewsletterCommand, NewsletterDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveNewsletterCommand(null, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<NewsletterDto>, ProblemHttpResult>> Update(Guid id, NewsletterInput body, ICommandHandler<SaveNewsletterCommand, NewsletterDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveNewsletterCommand(id, body), ct)).ToHttpResult();
    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ICommandHandler<DeleteNewsletterCommand> h, CancellationToken ct) =>
        (await h.Handle(new DeleteNewsletterCommand(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<RecipientCount>, ProblemHttpResult>> Count(Guid id, IQueryHandler<CountRecipientsQuery, RecipientCount> h, CancellationToken ct) =>
        (await h.Handle(new CountRecipientsQuery(id), ct)).ToHttpResult();
    private static async Task<Results<NoContent, ProblemHttpResult>> Test(Guid id, NewsletterTestInput body, ICommandHandler<TestNewsletterCommand> h, CancellationToken ct) =>
        (await h.Handle(new TestNewsletterCommand(id, body.Email), ct)).ToHttpResult();
    private static async Task<Results<Ok<NewsletterDto>, ProblemHttpResult>> Send(Guid id, NewsletterSendInput body, ICommandHandler<SendNewsletterCommand, NewsletterDto> h, CancellationToken ct) =>
        (await h.Handle(new SendNewsletterCommand(id, body.At), ct)).ToHttpResult();
    private static async Task<Results<Ok<NewsletterDto>, ProblemHttpResult>> Cancel(Guid id, ICommandHandler<CancelScheduleCommand, NewsletterDto> h, CancellationToken ct) =>
        (await h.Handle(new CancelScheduleCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<PublicNewsletterSummary>>, ProblemHttpResult>> Archive(IQueryHandler<ListPublicNewslettersQuery, IReadOnlyList<PublicNewsletterSummary>> h, CancellationToken ct) =>
        (await h.Handle(new ListPublicNewslettersQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<PublicNewsletter>, ProblemHttpResult>> Issue(string slug, IQueryHandler<GetPublicNewsletterQuery, PublicNewsletter> h, CancellationToken ct) =>
        (await h.Handle(new GetPublicNewsletterQuery(slug), ct)).ToHttpResult();
    // The token travels in the query (?t=): one-click unsubscribe POSTs a form body we don't need to read.
    private static async Task<Results<NoContent, ProblemHttpResult>> Unsubscribe(string? t, ICommandHandler<UnsubscribeNewsletterCommand> h, CancellationToken ct) =>
        (await h.Handle(new UnsubscribeNewsletterCommand(t ?? ""), ct)).ToHttpResult();
    private static async Task<Results<NoContent, ProblemHttpResult>> Confirm(string? t, ICommandHandler<ConfirmSubscriptionCommand> h, CancellationToken ct) =>
        (await h.Handle(new ConfirmSubscriptionCommand(t ?? ""), ct)).ToHttpResult();
}
