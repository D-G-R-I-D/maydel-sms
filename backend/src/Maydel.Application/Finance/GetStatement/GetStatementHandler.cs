using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Finance.GetStatement;

internal sealed class GetStatementHandler(IAppDbContext db) : IQueryHandler<GetStatementQuery, StudentStatement>
{
    public async Task<Result<StudentStatement>> Handle(GetStatementQuery q, CancellationToken ct) =>
        await Statements.ForAsync(db, q.StudentId, ct) is { } statement ? statement : FinanceErrors.StudentNotFound;
}
