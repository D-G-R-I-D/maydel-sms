using Maydel.Application.Abstractions;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Common.Behaviours;

/// <summary>
/// Wraps a command in one database transaction: committed on success, rolled back on a failed result or an exception.
/// Handlers that call SaveChanges several times (e.g. through UserManager) stay atomic.
/// Commands marked <see cref="INonTransactional"/> are passed straight through.
/// </summary>
internal static class TransactionDecorator
{
    internal sealed class Command<TCommand>(ICommandHandler<TCommand> inner, IAppDbContext db)
        : ICommandHandler<TCommand> where TCommand : ICommand
    {
        public Task<Result> Handle(TCommand command, CancellationToken ct) =>
            command is INonTransactional ? inner.Handle(command, ct) : RunAsync(db, () => inner.Handle(command, ct), ct);
    }

    internal sealed class Command<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> inner, IAppDbContext db)
        : ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
    {
        public Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct) =>
            command is INonTransactional ? inner.Handle(command, ct) : RunAsync(db, () => inner.Handle(command, ct), ct);
    }

    private static async Task<TResult> RunAsync<TResult>(IAppDbContext db, Func<Task<TResult>> next, CancellationToken ct) where TResult : Result
    {
        // A decorator further out (or a test) may already own the transaction.
        if (db.Database.CurrentTransaction is not null) return await next();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await next();
        if (result.IsSuccess) await transaction.CommitAsync(ct);
        else await transaction.RollbackAsync(ct);
        return result;
    }
}
