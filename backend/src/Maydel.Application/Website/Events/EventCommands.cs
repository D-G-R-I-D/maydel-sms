using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Website.Events;

/// <summary>Staff: events from a date (default: 30 days ago), soonest first.</summary>
public sealed record ListEventsQuery(DateTimeOffset? From = null) : IQuery<IReadOnlyList<EventDto>>;

public sealed record SaveEventCommand(Guid? Id, EventInput Event) : ICommand<EventDto>;

public sealed record DeleteEventCommand(Guid Id) : ICommand;

internal sealed class ListEventsHandler(IAppDbContext db, ISchoolClock clock) : IQueryHandler<ListEventsQuery, IReadOnlyList<EventDto>>
{
    public async Task<Result<IReadOnlyList<EventDto>>> Handle(ListEventsQuery q, CancellationToken ct)
    {
        var from = (q.From ?? clock.Now.AddDays(-30)).ToUniversalTime();
        return await db.Events.AsNoTracking().Where(e => (e.EndsAt ?? e.StartsAt) >= from).OrderBy(e => e.StartsAt)
            .Select(e => new EventDto(e.Id, e.Title, e.Description, e.StartsAt, e.EndsAt, e.Location, e.Audience, e.DescriptionHtml)).ToListAsync(ct);
    }
}

internal sealed class SaveEventHandler(IAppDbContext db) : ICommandHandler<SaveEventCommand, EventDto>
{
    public async Task<Result<EventDto>> Handle(SaveEventCommand c, CancellationToken ct)
    {
        SchoolEvent e;
        if (c.Id is { } id)
        {
            if (await db.Events.FirstOrDefaultAsync(x => x.Id == id, ct) is not { } found) return WebsiteErrors.EventNotFound;
            e = found;
        }
        else
        {
            db.Events.Add(e = new SchoolEvent { Title = "" });
        }
        var i = c.Event;
        (e.Title, e.Description, e.StartsAt, e.EndsAt, e.Location, e.Audience) = (i.Title ?? "", string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(),
            i.StartsAt.ToUniversalTime(), i.EndsAt?.ToUniversalTime(), string.IsNullOrWhiteSpace(i.Location) ? null : i.Location.Trim(), i.Audience);
        if (e.Check() is { } invalid) return invalid.ToError();
        if (i.DescriptionHtml is { Length: > RichText.MaxLength }) return Error.Validation("descriptionHtml", "The description is too long.");
        e.DescriptionHtml = null;
        if (RichText.Clean(i.DescriptionHtml) is { } rich) (e.DescriptionHtml, e.Description) = rich;
        else if (e.Description is { Length: > 4000 }) return Error.Validation("description", "Keep the description under 4000 characters.");
        if (e.Location is { Length: > 200 }) return Error.Validation("location", "Keep the place under 200 characters.");
        await db.SaveChangesAsync(ct);
        return new EventDto(e.Id, e.Title, e.Description, e.StartsAt, e.EndsAt, e.Location, e.Audience, e.DescriptionHtml);
    }
}

internal sealed class DeleteEventHandler(IAppDbContext db) : ICommandHandler<DeleteEventCommand>
{
    public async Task<Result> Handle(DeleteEventCommand c, CancellationToken ct)
    {
        if (await db.Events.FirstOrDefaultAsync(x => x.Id == c.Id, ct) is not { } e) return WebsiteErrors.EventNotFound;
        db.Events.Remove(e);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
