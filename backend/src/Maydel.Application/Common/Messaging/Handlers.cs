namespace Maydel.Application.Common.Messaging;

// Our own small CQRS contracts instead of MediatR (see ADR-004). Endpoints inject the handler
// interface directly; validation, logging and transactions are added as decorators.

/// <summary>Changes state and returns nothing on success.</summary>
public interface ICommand;

/// <summary>Changes state and returns <typeparamref name="TResponse"/> on success.</summary>
public interface ICommand<TResponse>;

/// <summary>Reads state. Must not change anything.</summary>
public interface IQuery<TResponse>;

/// <summary>
/// Opts a command out of the transaction decorator. Use only when a failed result must still persist
/// its side effects (e.g. counting a failed sign-in towards lockout, revoking a stolen token family).
/// </summary>
public interface INonTransactional;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken ct);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct);
}
