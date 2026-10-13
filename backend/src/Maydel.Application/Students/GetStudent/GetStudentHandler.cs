using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Common.Access;

namespace Maydel.Application.Students.GetStudent;

internal sealed class GetStudentHandler(IAppDbContext db, AccessRules access) : IQueryHandler<GetStudentQuery, StudentDetail>
{
    public async Task<Result<StudentDetail>> Handle(GetStudentQuery query, CancellationToken ct)
    {
        // Not found and not allowed look the same, so ids of other families' children can't be probed.
        if (!await access.CanViewStudentAsync(query.Id, ct)) return StudentErrors.NotFound;
        return await db.GetStudentDetailAsync(query.Id, ct) is { } student ? student : StudentErrors.NotFound;
    }
}
