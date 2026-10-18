using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetAttempt;

internal sealed class GetAttemptHandler(Attempts attempts) : IQueryHandler<GetAttemptQuery, AttemptPaper>
{
    public async Task<Result<AttemptPaper>> Handle(GetAttemptQuery q, CancellationToken ct)
    {
        if (await attempts.MeAsync(ct) is not { } student) return CbtErrors.NotAStudent;
        return await attempts.MineAsync(q.AttemptId, student, ct) is { } attempt
            ? await attempts.PaperAsync(attempt, includeToken: false, ct)
            : CbtErrors.AttemptNotFound;
    }
}
