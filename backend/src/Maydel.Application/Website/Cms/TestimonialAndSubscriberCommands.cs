using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.Cms;

public sealed record TestimonialInput(string Quote, string Attribution, int Order, bool IsPublished);

public sealed record TestimonialDto(Guid Id, string Quote, string Attribution, int Order, bool IsPublished);

public sealed record ListTestimonialsQuery : IQuery<IReadOnlyList<TestimonialDto>>;

public sealed record SaveTestimonialCommand(Guid? Id, TestimonialInput Testimonial) : ICommand<TestimonialDto>;

public sealed record DeleteTestimonialCommand(Guid Id) : ICommand;

/// <summary><c>Confirmed</c>: they clicked the link in the confirmation email (newsletters go only to confirmed, active subscribers).</summary>
public sealed record SubscriberDto(Guid Id, string Email, bool IsActive, DateTimeOffset CreatedAt, bool Confirmed);

/// <summary>Everyone by default, with their status; <c>active=true</c> for those who currently receive newsletters.</summary>
public sealed record ListSubscribersQuery(bool? Active = null, int? Page = null, int? PageSize = null) : IQuery<PagedResult<SubscriberDto>>;

/// <summary>Takes an address off the list (kept, marked inactive, so it isn't re-added silently by an import).</summary>
public sealed record UnsubscribeCommand(Guid Id) : ICommand;

internal sealed class ListTestimonialsHandler(IAppDbContext db) : IQueryHandler<ListTestimonialsQuery, IReadOnlyList<TestimonialDto>>
{
    public async Task<Result<IReadOnlyList<TestimonialDto>>> Handle(ListTestimonialsQuery q, CancellationToken ct) =>
        await db.Testimonials.AsNoTracking().OrderBy(t => t.Order).ThenBy(t => t.CreatedAt)
            .Select(t => new TestimonialDto(t.Id, t.Quote, t.Attribution, t.Order, t.IsPublished)).ToListAsync(ct);
}

internal sealed class SaveTestimonialHandler(IAppDbContext db) : ICommandHandler<SaveTestimonialCommand, TestimonialDto>
{
    public async Task<Result<TestimonialDto>> Handle(SaveTestimonialCommand c, CancellationToken ct)
    {
        var i = c.Testimonial;
        var quote = (i.Quote ?? "").Trim();
        var who = (i.Attribution ?? "").Trim();
        if (quote.Length is 0 or > 1000) return Error.Validation("quote", "Write the testimonial (up to 1000 characters).");
        if (who.Length is 0 or > 120) return Error.Validation("attribution", "Say who it's from, e.g. \"Parent of a Year 3 pupil\" (up to 120 characters).");
        Testimonial t;
        if (c.Id is { } id)
        {
            if (await db.Testimonials.FirstOrDefaultAsync(x => x.Id == id, ct) is not { } found) return Error.NotFound("testimonial.not_found", "That testimonial doesn't exist.");
            t = found;
        }
        else
        {
            db.Testimonials.Add(t = new Testimonial { Quote = "", Attribution = "" });
        }
        (t.Quote, t.Attribution, t.Order, t.IsPublished) = (quote, who, i.Order, i.IsPublished);
        await db.SaveChangesAsync(ct);
        return new TestimonialDto(t.Id, t.Quote, t.Attribution, t.Order, t.IsPublished);
    }
}

internal sealed class DeleteTestimonialHandler(IAppDbContext db) : ICommandHandler<DeleteTestimonialCommand>
{
    public async Task<Result> Handle(DeleteTestimonialCommand c, CancellationToken ct)
    {
        if (await db.Testimonials.FirstOrDefaultAsync(x => x.Id == c.Id, ct) is not { } t) return Error.NotFound("testimonial.not_found", "That testimonial doesn't exist.");
        db.Testimonials.Remove(t);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

internal sealed class ListSubscribersHandler(IAppDbContext db) : IQueryHandler<ListSubscribersQuery, PagedResult<SubscriberDto>>
{
    public async Task<Result<PagedResult<SubscriberDto>>> Handle(ListSubscribersQuery q, CancellationToken ct)
    {
        var list = db.NewsletterSubscribers.AsNoTracking();
        if (q.Active is { } active) list = list.Where(s => s.IsActive == active);
        return await list.OrderByDescending(s => s.CreatedAt).Select(s => new SubscriberDto(s.Id, s.Email, s.IsActive, s.CreatedAt, s.ConfirmedAt != null)).ToPagedAsync(q.Page, q.PageSize, ct);
    }
}

internal sealed class UnsubscribeHandler(IAppDbContext db) : ICommandHandler<UnsubscribeCommand>
{
    public async Task<Result> Handle(UnsubscribeCommand c, CancellationToken ct)
    {
        if (await db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.Id == c.Id, ct) is not { } s) return Error.NotFound("subscriber.not_found", "That subscriber doesn't exist.");
        s.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
