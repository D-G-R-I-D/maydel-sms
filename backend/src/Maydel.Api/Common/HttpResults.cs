using System.Diagnostics;
using Maydel.Application.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Maydel.Api.Common;

/// <summary>
/// Maps <see cref="Result"/> to typed HTTP results. Failures become RFC 9457 problem details carrying the error's
/// stable <c>code</c> and the request's <c>traceId</c>. Declare the possible error statuses on the endpoint with
/// <see cref="ProducesProblems"/> so the OpenAPI contract lists them.
/// </summary>
public static class HttpResults
{
    public static Results<Ok<T>, ProblemHttpResult> ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();

    public static Results<NoContent, ProblemHttpResult> ToHttpResult(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();

    public static ProblemHttpResult ToProblem(this Error error)
    {
        ProblemDetails problem = error is ValidationError validation
            ? new HttpValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();

        (problem.Status, problem.Title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "One or more fields are invalid."),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorType.Rule => (StatusCodes.Status422UnprocessableEntity, "Request could not be processed"),
            _ => throw new UnreachableException($"Unmapped error type {error.Type}."),
        };
        problem.Detail = error.Message;
        problem.Extensions["code"] = error.Code;
        return TypedResults.Problem(problem);
    }

    /// <summary>Documents the problem responses an endpoint can return (400 adds the validation shape).</summary>
    public static RouteHandlerBuilder ProducesProblems(this RouteHandlerBuilder builder, params int[] statusCodes)
    {
        foreach (var status in statusCodes)
        {
            if (status == StatusCodes.Status400BadRequest) builder.ProducesValidationProblem();
            else builder.ProducesProblem(status);
        }
        return builder;
    }
}
