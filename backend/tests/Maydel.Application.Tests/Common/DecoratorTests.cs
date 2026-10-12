using FluentValidation;
using Maydel.Application.Common;
using Maydel.Application.Common.Behaviours;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Tests.Infrastructure;
using Maydel.Domain.Academics;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Tests.Common;

[Collection(nameof(DatabaseCollection))]
public sealed class DecoratorTests(DatabaseFixture fixture)
{
    public sealed record CreateSubject(string Name, bool Fail) : ICommand<Guid>;

    private sealed class CreateSubjectHandler(Maydel.Application.Abstractions.IAppDbContext db) : ICommandHandler<CreateSubject, Guid>
    {
        public int Calls { get; private set; }

        public async Task<Result<Guid>> Handle(CreateSubject command, CancellationToken ct)
        {
            Calls++;
            var subject = new Subject { Name = command.Name, Code = command.Name[^8..] };
            db.Subjects.Add(subject);
            await db.SaveChangesAsync(ct);
            return command.Fail ? Error.Rule("test.failed", "Failed after writing.") : subject.Id;
        }
    }

    private sealed class CreateSubjectValidator : AbstractValidator<CreateSubject>
    {
        public CreateSubjectValidator() => RuleFor(x => x.Name).MinimumLength(8);
    }

    [Fact]
    public async Task Validation_failure_short_circuits_with_field_errors_in_camel_case()
    {
        await using var db = fixture.CreateContext(new TestUser());
        var inner = new CreateSubjectHandler(db);
        var handler = new ValidationDecorator.Command<CreateSubject, Guid>(inner, [new CreateSubjectValidator()]);

        var result = await handler.Handle(new CreateSubject("short", Fail: false), default);

        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains("name", error.Errors.Keys);
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task Transaction_commits_on_success()
    {
        var name = Unique();
        await using (var db = fixture.CreateContext(new TestUser()))
        {
            var handler = new TransactionDecorator.Command<CreateSubject, Guid>(new CreateSubjectHandler(db), db);
            Assert.True((await handler.Handle(new CreateSubject(name, Fail: false), default)).IsSuccess);
        }

        await using var check = fixture.CreateContext(new TestUser());
        Assert.True(await check.Subjects.AnyAsync(s => s.Name == name));
    }

    [Fact]
    public async Task Transaction_rolls_back_writes_when_the_handler_returns_a_failure()
    {
        var name = Unique();
        await using (var db = fixture.CreateContext(new TestUser()))
        {
            var handler = new TransactionDecorator.Command<CreateSubject, Guid>(new CreateSubjectHandler(db), db);
            Assert.True((await handler.Handle(new CreateSubject(name, Fail: true), default)).IsFailure);
        }

        await using var check = fixture.CreateContext(new TestUser());
        Assert.False(await check.Subjects.AnyAsync(s => s.Name == name));
        // The audit row written in the same transaction is rolled back too.
        var subjectAudits = await check.AuditLogs.Where(l => l.EntityType == nameof(Subject)).Select(l => l.Changes).ToListAsync();
        Assert.DoesNotContain(subjectAudits, changes => changes!.Contains(name));
    }

    private static string Unique() => "Subject " + Guid.NewGuid().ToString("N");
}
