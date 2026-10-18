using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.DeleteTest;

internal sealed class DeleteTestHandler(IAppDbContext db, Tests tests) : ICommandHandler<DeleteTestCommand>
{
    public async Task<Result> Handle(DeleteTestCommand c, CancellationToken ct)
    {
        if (await tests.ManageableAsync(c.Id, ct) is not { } test) return CbtErrors.TestNotFound;
        if (test.CanEdit() is { } locked) return locked.ToError();
        db.CbtTests.Remove(test);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
