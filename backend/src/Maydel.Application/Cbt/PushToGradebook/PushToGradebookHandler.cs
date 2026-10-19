using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.PushToGradebook;

internal sealed class PushToGradebookHandler(IAppDbContext db, Tests tests, Gradebook gradebook, Attempts attempts, ICurrentUser me)
    : ICommandHandler<PushToGradebookCommand, PushResult>
{
    public async Task<Result<PushResult>> Handle(PushToGradebookCommand c, CancellationToken ct)
    {
        if (await tests.ManageableAsync(c.TestId, ct) is not { } test) return CbtErrors.TestNotFound;
        if (test.ResultsReleasedAt is null) return CbtErrors.NotReleased;
        if (await gradebook.PreviewAsync(test, ct) is not { } preview) return CbtErrors.NoComponent;
        var (pushed, skipped) = await gradebook.PushAsync(test, preview.Preview, preview.Component, c.StudentIds?.ToHashSet(), me.UserId, ct);
        if (pushed > 0) test.GradebookPushedAt = attempts.Now;
        await db.SaveChangesAsync(ct);
        return new PushResult(pushed, skipped);
    }
}
