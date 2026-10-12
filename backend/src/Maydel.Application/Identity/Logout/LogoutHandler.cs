using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Identity.Logout;

internal sealed class LogoutHandler(SessionService sessions) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken ct)
    {
        await sessions.RevokeAsync(command.RefreshToken, ct);
        return Result.Success();
    }
}
