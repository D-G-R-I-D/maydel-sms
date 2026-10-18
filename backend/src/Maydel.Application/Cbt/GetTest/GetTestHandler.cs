using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.GetTest;

internal sealed class GetTestHandler(Tests tests) : IQueryHandler<GetTestQuery, TestDetail>
{
    public async Task<Result<TestDetail>> Handle(GetTestQuery q, CancellationToken ct) =>
        await tests.ManageableAsync(q.Id, ct, track: false) is null ? CbtErrors.TestNotFound : await tests.DetailAsync(q.Id, ct);
}
