using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.PublishTest;

internal sealed class PublishTestHandler(IAppDbContext db, Tests tests, ISchoolClock clock) : ICommandHandler<PublishTestCommand, TestDetail>
{
    public async Task<Result<TestDetail>> Handle(PublishTestCommand c, CancellationToken ct)
    {
        if (await tests.ManageableAsync(c.Id, ct) is not { } test) return CbtErrors.TestNotFound;
        var invalid = c.Publish
            ? test.Publish(clock.Now.ToUniversalTime(), await tests.ProblemsAsync(test, ct))
            : test.Unpublish(anyAttempts: false);
        if (invalid is not null) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return await tests.DetailAsync(test.Id, ct);
    }
}
