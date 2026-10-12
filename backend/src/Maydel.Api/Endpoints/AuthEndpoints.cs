using Maydel.Api.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Application.Identity.ChangePassword;
using Maydel.Application.Identity.GetMe;
using Maydel.Application.Identity.GoogleLogin;
using Maydel.Application.Identity.Login;
using Maydel.Application.Identity.Logout;
using Maydel.Application.Identity.RefreshSession;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maydel.Api.Endpoints;

/// <summary>
/// Called server-to-server by the Next.js BFF, which keeps the tokens in httpOnly cookies (ADR-002).
/// The BFF forwards the browser's IP in X-Forwarded-For and its User-Agent.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var anonymous = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous().RequireRateLimiting(RateLimits.Auth);
        var signedIn = app.MapGroup("/api/auth").WithTags("Auth").RequireAuthorization();

        anonymous.MapPost("/login", Login).WithName("Login").ProducesProblems(400, 401, 429);
        anonymous.MapPost("/google", GoogleLogin).WithName("GoogleLogin").ProducesProblems(400, 401, 404, 429);
        anonymous.MapPost("/refresh", Refresh).WithName("RefreshSession").ProducesProblems(400, 401, 429);
        anonymous.MapPost("/logout", Logout).WithName("Logout").ProducesProblems(400, 429);

        signedIn.MapGet("/me", GetMe).WithName("GetMe").ProducesProblems(401);
        signedIn.MapPost("/change-password", ChangePassword).WithName("ChangePassword")
            .RequireRateLimiting(RateLimits.Auth).ProducesProblems(400, 401, 429);
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> Login(
        LoginCommand command, ICommandHandler<LoginCommand, AuthResponse> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> GoogleLogin(
        GoogleLoginCommand command, ICommandHandler<GoogleLoginCommand, AuthResponse> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> Refresh(
        RefreshSessionCommand command, ICommandHandler<RefreshSessionCommand, AuthResponse> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> Logout(
        LogoutCommand command, ICommandHandler<LogoutCommand> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();

    private static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> GetMe(
        IQueryHandler<GetMeQuery, MeResponse> handler, CancellationToken ct) =>
        (await handler.Handle(new GetMeQuery(), ct)).ToHttpResult();

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> ChangePassword(
        ChangePasswordCommand command, ICommandHandler<ChangePasswordCommand, AuthResponse> handler, CancellationToken ct) =>
        (await handler.Handle(command, ct)).ToHttpResult();
}
