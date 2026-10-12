using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Identity.RefreshSession;

/// <summary>Rotates a refresh token: the old one is revoked and a new access + refresh pair is returned.</summary>
public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<AuthResponse>, INonTransactional;
