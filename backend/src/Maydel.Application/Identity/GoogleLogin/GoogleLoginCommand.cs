using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Identity.GoogleLogin;

/// <summary>Sign in with a Google ID token obtained in the browser via Google Identity Services.</summary>
public sealed record GoogleLoginCommand(string IdToken) : ICommand<AuthResponse>;
