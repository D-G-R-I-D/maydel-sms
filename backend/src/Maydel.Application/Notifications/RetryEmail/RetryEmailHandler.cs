using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Notifications.RetryEmail;

internal sealed class RetryEmailHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<RetryEmailCommand>
{
    public async Task<Result> Handle(RetryEmailCommand c, CancellationToken ct)
    {
        var email = await db.OutboxEmails.FirstOrDefaultAsync(e => e.Id == c.EmailId, ct);
        if (email is null) return NotificationErrors.EmailNotFound;
        if (email.Retry(clock.GetUtcNow()) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
