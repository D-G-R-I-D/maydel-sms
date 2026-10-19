using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.ReleaseResults;

internal sealed class ReleaseResultsHandler(IAppDbContext db, Tests tests, Results results, Attempts attempts) : ICommandHandler<ReleaseResultsCommand, TestDetail>
{
    public async Task<Result<TestDetail>> Handle(ReleaseResultsCommand c, CancellationToken ct)
    {
        if (await tests.ManageableAsync(c.TestId, ct) is not { } test) return CbtErrors.TestNotFound;
        if (test.ReleaseResults(attempts.Now, await results.AttemptsAsync(test.Id, ct)) is { } invalid) return invalid.ToError();
        await db.SaveChangesAsync(ct);
        return await tests.DetailAsync(test.Id, ct);
    }
}
