using System.Diagnostics;
using Maydel.Application.Common.Messaging;
using Microsoft.Extensions.Logging;

namespace Maydel.Application.Common.Behaviours;

/// <summary>
/// Logs the use case name, outcome and duration. Never logs the request itself: it may contain passwords or personal data.
/// </summary>
internal static class LoggingDecorator
{
    internal sealed class Command<TCommand>(ICommandHandler<TCommand> inner, ILogger<TCommand> logger)
        : ICommandHandler<TCommand> where TCommand : ICommand
    {
        public Task<Result> Handle(TCommand command, CancellationToken ct) => LogAsync(logger, typeof(TCommand).Name, () => inner.Handle(command, ct));
    }

    internal sealed class Command<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> inner, ILogger<TCommand> logger)
        : ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
    {
        public Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct) => LogAsync(logger, typeof(TCommand).Name, () => inner.Handle(command, ct));
    }

    internal sealed class Query<TQuery, TResponse>(IQueryHandler<TQuery, TResponse> inner, ILogger<TQuery> logger)
        : IQueryHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
    {
        public Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct) => LogAsync(logger, typeof(TQuery).Name, () => inner.Handle(query, ct));
    }

    private static async Task<TResult> LogAsync<TResult>(ILogger logger, string name, Func<Task<TResult>> next) where TResult : Result
    {
        var started = Stopwatch.GetTimestamp();
        var result = await next();
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (result.IsSuccess)
            logger.LogInformation("{UseCase} succeeded in {ElapsedMs:0} ms", name, elapsed);
        else
            logger.LogInformation("{UseCase} failed with {ErrorType} {ErrorCode} in {ElapsedMs:0} ms", name, result.Error!.Type, result.Error.Code, elapsed);
        return result;
    }
}
