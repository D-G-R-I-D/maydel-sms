using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Identity.Login;

/// <summary>Sign in with a username (admission number for students) or email, and a password.</summary>
public sealed record LoginCommand(string UserName, string Password) : ICommand<AuthResponse>, INonTransactional;
