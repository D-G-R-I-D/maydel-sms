using Maydel.Api.Common;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Photos;
using Maydel.Application.Website;
using Maydel.Application.Website.Contact;
using Maydel.Application.Website.Events;
using Maydel.Application.Website.Gallery;
using Maydel.Application.Website.GetPost;
using Maydel.Application.Website.ListPosts;
using Maydel.Application.Website.Public;
using Maydel.Application.Website.SavePost;
using Maydel.Application.Website.StaffProfiles;
using Maydel.Domain.Admissions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Maydel.Api.Endpoints;

/// <summary>The public website's content (E7): admin CMS under /api/cms, anonymous reads under /api/public/site.</summary>
public static class WebsiteEndpoints
{
    public static void MapWebsiteEndpoints(this IEndpointRouteBuilder app)
    {
        var cms = app.MapGroup("/api/cms").WithTags("Website CMS").RequireAuthorization(Policies.Admin);
        cms.MapGet("/posts", ListPosts).WithName("ListPosts").ProducesProblems(401, 403);
        cms.MapGet("/posts/{id:guid}", GetPost).WithName("GetPost").ProducesProblems(401, 403, 404);
        cms.MapPost("/posts", CreatePost).WithName("CreatePost").ProducesProblems(400, 401, 403);
        cms.MapPut("/posts/{id:guid}", UpdatePost).WithName("UpdatePost").ProducesProblems(400, 401, 403, 404);
        cms.MapPost("/posts/{id:guid}/publish", Publish).WithName("PublishPost").ProducesProblems(401, 403, 404);
        cms.MapPost("/posts/{id:guid}/unpublish", Unpublish).WithName("UnpublishPost").ProducesProblems(401, 403, 404);
        cms.MapDelete("/posts/{id:guid}", DeletePost).WithName("DeletePost").ProducesProblems(401, 403, 404);
        cms.MapPut("/posts/{id:guid}/image", SetPostImage).WithName("SetPostImage").Upload();
        cms.MapDelete("/posts/{id:guid}/image", RemovePostImage).WithName("RemovePostImage").ProducesProblems(400, 401, 403, 404);
        cms.MapGet("/events", Events).WithName("ListEvents").ProducesProblems(401, 403);
        cms.MapPost("/events", CreateEvent).WithName("CreateEvent").ProducesProblems(400, 401, 403);
        cms.MapPut("/events/{id:guid}", UpdateEvent).WithName("UpdateEvent").ProducesProblems(400, 401, 403, 404);
        cms.MapDelete("/events/{id:guid}", DeleteEvent).WithName("DeleteEvent").ProducesProblems(401, 403, 404);
        cms.MapGet("/gallery", Photos).WithName("ListGalleryPhotos").ProducesProblems(401, 403);
        cms.MapPost("/gallery", AddPhoto).WithName("AddGalleryPhoto").Upload();
        cms.MapPut("/gallery/{id:guid}", UpdatePhoto).WithName("UpdateGalleryPhoto").ProducesProblems(400, 401, 403, 404);
        cms.MapDelete("/gallery/{id:guid}", DeletePhoto).WithName("DeleteGalleryPhoto").ProducesProblems(401, 403, 404);
        cms.MapGet("/messages", Messages).WithName("ListContactMessages").ProducesProblems(401, 403);
        cms.MapPut("/messages/{id:guid}/status", MessageStatusSet).WithName("SetContactMessageStatus").ProducesProblems(400, 401, 403, 404);
        cms.MapPut("/staff/{id:guid}", StaffProfile).WithName("SetStaffWebsiteProfile").ProducesProblems(400, 401, 403, 404);

        var site = app.MapGroup("/api/public/site").WithTags("Website").AllowAnonymous();
        site.MapGet("/news", News).WithName("ListPublicNews").ProducesProblems(400);
        site.MapGet("/news/{slug}", Article).WithName("GetPublicNews").ProducesProblems(404);
        site.MapGet("/notices", Notices).WithName("ListPublicNotices");
        site.MapGet("/highlights", Highlights).WithName("GetHighlights");
        site.MapGet("/events", PublicEvents).WithName("ListPublicEvents");
        site.MapGet("/gallery", Gallery).WithName("GetPublicGallery");
        site.MapGet("/staff", Staff).WithName("ListPublicStaff");
        site.MapPost("/contact", Contact).WithName("SendContactMessage").RequireRateLimiting(RateLimits.PublicForms).ProducesProblems(400, 429);
        site.MapGet("/images/{kind}/{id:guid}", Image).WithName("GetPublicImage").ProducesProblems(404).PublicCache();
    }

    /// <summary>Public website images: versioned URLs (?v=) so shared caches may keep them for a week.</summary>
    private static RouteHandlerBuilder PublicCache(this RouteHandlerBuilder builder) => builder.AddEndpointFilter(async (context, next) =>
    {
        var response = context.HttpContext.Response;
        response.OnStarting(() =>
        {
            response.Headers.CacheControl = response.StatusCode is 200 or 304 ? "public, max-age=604800" : "no-store";
            return Task.CompletedTask;
        });
        return await next(context);
    });

    public sealed record StatusInput(MessageStatus Status);

    private static async Task<Results<Ok<PagedResult<PostDto>>, ProblemHttpResult>> ListPosts([AsParameters] ListPostsQuery q, IQueryHandler<ListPostsQuery, PagedResult<PostDto>> h, CancellationToken ct) =>
        (await h.Handle(q, ct)).ToHttpResult();
    private static async Task<Results<Ok<PostDto>, ProblemHttpResult>> GetPost(Guid id, IQueryHandler<GetPostQuery, PostDto> h, CancellationToken ct) =>
        (await h.Handle(new GetPostQuery(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<PostDto>, ProblemHttpResult>> CreatePost(PostInput body, ICommandHandler<SavePostCommand, PostDto> h, CancellationToken ct) =>
        (await h.Handle(new SavePostCommand(null, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<PostDto>, ProblemHttpResult>> UpdatePost(Guid id, PostInput body, ICommandHandler<SavePostCommand, PostDto> h, CancellationToken ct) =>
        (await h.Handle(new SavePostCommand(id, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<PostDto>, ProblemHttpResult>> Publish(Guid id, ICommandHandler<PublishPostCommand, PostDto> h, CancellationToken ct) =>
        (await h.Handle(new PublishPostCommand(id, true), ct)).ToHttpResult();
    private static async Task<Results<Ok<PostDto>, ProblemHttpResult>> Unpublish(Guid id, ICommandHandler<PublishPostCommand, PostDto> h, CancellationToken ct) =>
        (await h.Handle(new PublishPostCommand(id, false), ct)).ToHttpResult();
    private static async Task<Results<NoContent, ProblemHttpResult>> DeletePost(Guid id, ICommandHandler<DeletePostCommand> h, CancellationToken ct) =>
        (await h.Handle(new DeletePostCommand(id), ct)).ToHttpResult();
    private static async Task<Results<Ok<PostDto>, ProblemHttpResult>> SetPostImage(Guid id, IFormFile? file, ICommandHandler<SetPostImageCommand, PostDto> h, CancellationToken ct) =>
        await PhotoEndpoints.ReadAsync(file, ct) is { } bytes ? (await h.Handle(new SetPostImageCommand(id, bytes), ct)).ToHttpResult() : PhotoErrors.Invalid.ToProblem();
    private static async Task<Results<Ok<PostDto>, ProblemHttpResult>> RemovePostImage(Guid id, ICommandHandler<SetPostImageCommand, PostDto> h, CancellationToken ct) =>
        (await h.Handle(new SetPostImageCommand(id, null), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<EventDto>>, ProblemHttpResult>> Events(DateTimeOffset? from, IQueryHandler<ListEventsQuery, IReadOnlyList<EventDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListEventsQuery(from), ct)).ToHttpResult();
    private static async Task<Results<Ok<EventDto>, ProblemHttpResult>> CreateEvent(EventInput body, ICommandHandler<SaveEventCommand, EventDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveEventCommand(null, body), ct)).ToHttpResult();
    private static async Task<Results<Ok<EventDto>, ProblemHttpResult>> UpdateEvent(Guid id, EventInput body, ICommandHandler<SaveEventCommand, EventDto> h, CancellationToken ct) =>
        (await h.Handle(new SaveEventCommand(id, body), ct)).ToHttpResult();
    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteEvent(Guid id, ICommandHandler<DeleteEventCommand> h, CancellationToken ct) =>
        (await h.Handle(new DeleteEventCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<GalleryPhotoDto>>, ProblemHttpResult>> Photos(string? category, IQueryHandler<ListPhotosQuery, IReadOnlyList<GalleryPhotoDto>> h, CancellationToken ct) =>
        (await h.Handle(new ListPhotosQuery(category), ct)).ToHttpResult();
    private static async Task<Results<Ok<GalleryPhotoDto>, ProblemHttpResult>> AddPhoto(
        IFormFile? file, [FromForm] string title, [FromForm] string? caption, [FromForm] string category, [FromForm] int? order, [FromForm] bool? isFeatured,
        ICommandHandler<AddPhotoCommand, GalleryPhotoDto> h, CancellationToken ct) =>
        await PhotoEndpoints.ReadAsync(file, ct) is { } bytes
            ? (await h.Handle(new AddPhotoCommand(bytes, new PhotoInput(title, caption, category, order ?? 0, isFeatured ?? false)), ct)).ToHttpResult()
            : PhotoErrors.Invalid.ToProblem();
    private static async Task<Results<Ok<GalleryPhotoDto>, ProblemHttpResult>> UpdatePhoto(Guid id, PhotoInput body, ICommandHandler<UpdatePhotoCommand, GalleryPhotoDto> h, CancellationToken ct) =>
        (await h.Handle(new UpdatePhotoCommand(id, body), ct)).ToHttpResult();
    private static async Task<Results<NoContent, ProblemHttpResult>> DeletePhoto(Guid id, ICommandHandler<DeletePhotoCommand> h, CancellationToken ct) =>
        (await h.Handle(new DeletePhotoCommand(id), ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<ContactMessageDto>>, ProblemHttpResult>> Messages([AsParameters] ListContactMessagesQuery q, IQueryHandler<ListContactMessagesQuery, PagedResult<ContactMessageDto>> h, CancellationToken ct) =>
        (await h.Handle(q, ct)).ToHttpResult();
    private static async Task<Results<Ok<ContactMessageDto>, ProblemHttpResult>> MessageStatusSet(Guid id, StatusInput body, ICommandHandler<SetContactStatusCommand, ContactMessageDto> h, CancellationToken ct) =>
        (await h.Handle(new SetContactStatusCommand(id, body.Status), ct)).ToHttpResult();
    private static async Task<Results<Ok<PublicStaff>, ProblemHttpResult>> StaffProfile(Guid id, StaffWebsiteInput body, ICommandHandler<SetStaffWebsiteCommand, PublicStaff> h, CancellationToken ct) =>
        (await h.Handle(new SetStaffWebsiteCommand(id, body), ct)).ToHttpResult();

    private static async Task<Results<Ok<PagedResult<NewsSummary>>, ProblemHttpResult>> News([AsParameters] ListNewsQuery q, IQueryHandler<ListNewsQuery, PagedResult<NewsSummary>> h, CancellationToken ct) =>
        (await h.Handle(q, ct)).ToHttpResult();
    private static async Task<Results<Ok<NewsArticle>, ProblemHttpResult>> Article(string slug, IQueryHandler<GetNewsQuery, NewsArticle> h, CancellationToken ct) =>
        (await h.Handle(new GetNewsQuery(slug), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<PublicNotice>>, ProblemHttpResult>> Notices(IQueryHandler<ListNoticesQuery, IReadOnlyList<PublicNotice>> h, CancellationToken ct) =>
        (await h.Handle(new ListNoticesQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<Highlights>, ProblemHttpResult>> Highlights(IQueryHandler<GetHighlightsQuery, Highlights> h, CancellationToken ct) =>
        (await h.Handle(new GetHighlightsQuery(), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<PublicEvent>>, ProblemHttpResult>> PublicEvents(int? take, IQueryHandler<ListPublicEventsQuery, IReadOnlyList<PublicEvent>> h, CancellationToken ct) =>
        (await h.Handle(new ListPublicEventsQuery(take ?? 20), ct)).ToHttpResult();
    private static async Task<Results<Ok<PublicGallery>, ProblemHttpResult>> Gallery(string? category, IQueryHandler<GetGalleryQuery, PublicGallery> h, CancellationToken ct) =>
        (await h.Handle(new GetGalleryQuery(category), ct)).ToHttpResult();
    private static async Task<Results<Ok<IReadOnlyList<PublicStaff>>, ProblemHttpResult>> Staff(IQueryHandler<ListPublicStaffQuery, IReadOnlyList<PublicStaff>> h, CancellationToken ct) =>
        (await h.Handle(new ListPublicStaffQuery(), ct)).ToHttpResult();
    private static async Task<Results<NoContent, ProblemHttpResult>> Contact(ContactInput body, ICommandHandler<SendContactMessageCommand> h, CancellationToken ct) =>
        (await h.Handle(new SendContactMessageCommand(body), ct)).ToHttpResult();
    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> Image(string kind, Guid id, IQueryHandler<GetPublicImageQuery, PhotoContent> h, CancellationToken ct) =>
        Enum.TryParse<PublicImageKind>(kind, ignoreCase: true, out var k)
            ? PhotoEndpoints.ToFile(await h.Handle(new GetPublicImageQuery(k, id), ct))
            : Error.NotFound("image.not_found", "No such picture.").ToProblem();
}
