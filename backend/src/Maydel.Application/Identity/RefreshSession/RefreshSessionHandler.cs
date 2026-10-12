using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Identity.RefreshSession;

// Non-transactional: when reuse is detected, the family revocation must persist even though the call fails.
internal sealed class RefreshSessionHandler(SessionService sessions) : ICommandHandler<RefreshSessionCommand, AuthResponse>
{
    public Task<Result<AuthResponse>> Handle(RefreshSessionCommand command, CancellationToken ct) =>
        sessions.RotateAsync(command.RefreshToken, ct);
}
