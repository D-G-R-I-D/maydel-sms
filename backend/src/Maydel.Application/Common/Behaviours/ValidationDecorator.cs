using FluentValidation;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Common.Behaviours;

/// <summary>Runs every FluentValidation validator for the request and short-circuits with a <see cref="ValidationError"/>.</summary>
internal static class ValidationDecorator
{
    internal sealed class Command<TCommand>(ICommandHandler<TCommand> inner, IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand> where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken ct) =>
            await ValidateAsync(command, validators, ct) is { } error ? error : await inner.Handle(command, ct);
    }

    internal sealed class Command<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> inner, IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct) =>
            await ValidateAsync(command, validators, ct) is { } error ? error : await inner.Handle(command, ct);
    }

    internal sealed class Query<TQuery, TResponse>(IQueryHandler<TQuery, TResponse> inner, IEnumerable<IValidator<TQuery>> validators)
        : IQueryHandler<TQuery, TResponse> where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct) =>
            await ValidateAsync(query, validators, ct) is { } error ? error : await inner.Handle(query, ct);
    }

    private static async Task<ValidationError?> ValidateAsync<T>(T request, IEnumerable<IValidator<T>> validators, CancellationToken ct)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
            failures.AddRange((await validator.ValidateAsync(request, ct)).Errors);

        if (failures.Count == 0) return null;
        return new ValidationError(failures
            .GroupBy(f => ToCamelCase(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray()));
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
