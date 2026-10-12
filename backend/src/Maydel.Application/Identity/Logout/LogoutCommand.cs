using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Identity.Logout;

/// <summary>Signs out the device holding this refresh token (its whole token family is revoked).</summary>
public sealed record LogoutCommand(string RefreshToken) : ICommand;
