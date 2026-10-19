using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Cbt.GetAttemptReview;

internal sealed class GetAttemptReviewHandler(IAppDbContext db, Tests tests, Attempts attempts, Results results) : IQueryHandler<GetAttemptReviewQuery, AttemptReview>
{
    public async Task<Result<AttemptReview>> Handle(GetAttemptReviewQuery q, CancellationToken ct)
    {
        var attempt = await db.TestAttempts.FirstOrDefaultAsync(a => a.Id == q.AttemptId, ct);
        if (attempt is null || await tests.ManageableAsync(attempt.TestId, ct, track: false) is not { } test) return CbtErrors.AttemptNotFound;
        await attempts.CloseIfOverdueAsync(attempt, ct);
        return await results.ReviewAsync(attempt, test, ct);
    }
}
