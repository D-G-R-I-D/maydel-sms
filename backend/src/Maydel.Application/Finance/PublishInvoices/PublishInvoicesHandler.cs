using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.PublishInvoices;

internal sealed class PublishInvoicesHandler(IAppDbContext db, InvoiceEmails emails, TimeProvider time) : ICommandHandler<PublishInvoicesCommand, PublishInvoicesResult>
{
    public async Task<Result<PublishInvoicesResult>> Handle(PublishInvoicesCommand c, CancellationToken ct)
    {
        if (!await db.Terms.AnyAsync(t => t.Id == c.TermId, ct)) return FinanceErrors.TermNotFound;
        if (c.ClassLevelId is { } level && !await db.ClassLevels.AnyAsync(l => l.Id == level, ct)) return FinanceErrors.ClassLevelNotFound;

        var query = db.Invoices.Where(i => i.TermId == c.TermId && !i.IsCancelled);
        if (c.ClassLevelId is { } l)
        {
            var rooms = db.ClassRooms.Where(r => r.ClassLevelId == l).Select(r => (Guid?)r.Id);
            query = query.Where(i => rooms.Contains(i.ClassRoomId));
        }
        var invoices = await query.ToListAsync(ct);
        var now = time.GetUtcNow();
        var published = invoices.Where(i => i.Publish(now)).ToList();
        var queued = (c.NotifyParents ?? true) ? await emails.QueueAsync(published, now, ct) : 0;
        await db.SaveChangesAsync(ct);
        return new PublishInvoicesResult(published.Count, invoices.Count - published.Count, queued);
    }
}
