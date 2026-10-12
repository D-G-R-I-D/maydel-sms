namespace Maydel.Application.Common;

public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,

    /// <summary>A business rule was violated. The message is shown to the user as-is.</summary>
    Rule,
}

/// <summary>An expected failure. <see cref="Code"/> is stable and machine-readable; <see cref="Message"/> is user-presentable.</summary>
public record Error(ErrorType Type, string Code, string Message)
{
    public static Error Unauthorized(string code, string message) => new(ErrorType.Unauthorized, code, message);
    public static Error Forbidden(string code = "forbidden", string message = "You do not have access to this resource.") => new(ErrorType.Forbidden, code, message);
    public static Error NotFound(string code = "not_found", string message = "The requested resource was not found.") => new(ErrorType.NotFound, code, message);
    public static Error Conflict(string code, string message) => new(ErrorType.Conflict, code, message);
    public static Error Rule(string code, string message) => new(ErrorType.Rule, code, message);
    public static ValidationError Validation(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>Field-level validation failures, keyed by camelCase field name.</summary>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors)
    : Error(ErrorType.Validation, "validation", "One or more fields are invalid.");

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;
    private Result(Error error) : base(error) { }

    /// <summary>The success value. Throws when read on a failure, which is always a programming error.</summary>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error!.Code}).");

    public static Result<T> Success(T value) => new(value);
    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}
