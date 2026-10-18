using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;

namespace Maydel.Application.Cbt.SaveTest;

internal sealed class SaveTestHandler(IAppDbContext db, Tests tests, ICurrentUser me) : ICommandHandler<SaveTestCommand, TestDetail>
{
    public async Task<Result<TestDetail>> Handle(SaveTestCommand c, CancellationToken ct)
    {
        CbtTest test;
        if (c.Id is { } id)
        {
            if (await tests.ManageableAsync(id, ct) is not { } found) return CbtErrors.TestNotFound;
            if (found.CanEdit() is { } locked) return locked.ToError();
            test = found;
        }
        else
        {
            test = new CbtTest { Title = "", CreatedByUserId = me.UserId };
        }
        if (await tests.CheckHeaderAsync(c.Test, ct) is { } bad) return bad;
        // Moving a test to another subject or level would leave fixed questions behind; start a new test instead.
        if (c.Id is not null && test.Sections.Count > 0 && (test.SubjectId != c.Test.SubjectId || test.ClassLevelId != c.Test.ClassLevelId))
            return Error.Validation("subjectId", "This test already has sections, so its subject and class level can't change.");

        Tests.ApplyHeader(test, c.Test);
        if (test.CheckSchedule() is { } invalid) return invalid.ToError();
        if (c.Id is null) db.CbtTests.Add(test);
        await db.SaveChangesAsync(ct);
        return await tests.DetailAsync(test.Id, ct);
    }
}
