using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Identity.ChangePassword;

/// <summary>
/// Changes the signed-in user's password, clears <c>MustChangePassword</c> and signs out every other device.
/// Returns a fresh session for the current device.
/// </summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand<AuthResponse>;
