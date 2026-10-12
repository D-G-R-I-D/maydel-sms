using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.ModuleName.UseCaseName;

#if (isQuery)
internal sealed class UseCaseNameHandler(IAppDbContext db, AccessRules access) : IQueryHandler<UseCaseNameQuery, ResponseType>
{
    public async Task<Result<ResponseType>> Handle(UseCaseNameQuery query, CancellationToken ct)
    {
        // Row-level access first for anything student-scoped, then AsNoTracking + project to the DTO.
        throw new NotImplementedException();
    }
}
#else
internal sealed class UseCaseNameHandler(IAppDbContext db, AccessRules access) : ICommandHandler<UseCaseNameCommand, ResponseType>
{
    public async Task<Result<ResponseType>> Handle(UseCaseNameCommand command, CancellationToken ct)
    {
        // Row-level access → load → apply domain rules → SaveChangesAsync. Return Error.* for expected failures.
        throw new NotImplementedException();
    }
}
#endif
