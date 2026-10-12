using Maydel.Application.Identity;
using Maydel.Domain.Common;
using Maydel.Infrastructure.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Maydel.Api.Common;

/// <summary>
/// Turns unexpected exceptions into problem responses with a trace id. Stack traces and messages never reach the client.
/// Expected failures should be returned as <c>Result</c> errors instead; the mappings below are a safety net.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problems, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        (int status, string title, string? detail) = exception switch
        {
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "Request could not be processed", e.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict", "The record was changed by someone else. Reload and try again."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "Conflict", "A record with the same unique value already exists."),
            BadHttpRequestException e => (e.StatusCode, "Bad request", "The request could not be read."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "Please try again. If it keeps happening, contact support with the trace id."),
        };

        if (status >= 500) logger.LogError(exception, "Unhandled exception for {Method} {Path}", http.Request.Method, http.Request.Path);
        else logger.LogWarning(exception, "Request failed with {Status} for {Method} {Path}", status, http.Request.Method, http.Request.Path);

        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            // Deliberately not passing the exception: in Development the default writer would add its details to the response.
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}

/// <summary>
/// Users holding a temporary password may only use <c>/api/auth/*</c> until they change it (E1.4).
/// The UI routes them to the change-password screen; this makes the API enforce it too.
/// </summary>
public sealed class PasswordChangeGate(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, IProblemDetailsService problems)
    {
        if (http.User.HasClaim(AppClaims.PasswordChangeRequired, "true") && !http.Request.Path.StartsWithSegments("/api/auth"))
        {
            var error = IdentityErrors.PasswordChangeRequired;
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problems.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails = { Status = StatusCodes.Status403Forbidden, Title = "Forbidden", Detail = error.Message, Extensions = { ["code"] = error.Code } },
            });
            return;
        }
        await next(http);
    }
}

public static class SecurityHeaders
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        if (!ctx.Request.Path.StartsWithSegments("/scalar") && !ctx.Request.Path.StartsWithSegments("/openapi"))
            h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        await next();
    });
}
