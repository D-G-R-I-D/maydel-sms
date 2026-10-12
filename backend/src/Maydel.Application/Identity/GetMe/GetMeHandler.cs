using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Microsoft.AspNetCore.Identity;

namespace Maydel.Application.Identity.GetMe;

internal sealed class GetMeHandler(ICurrentUser me, UserManager<AppUser> users) : IQueryHandler<GetMeQuery, MeResponse>
{
    public async Task<Result<MeResponse>> Handle(GetMeQuery query, CancellationToken ct)
    {
        if (me.UserId is not { } id || await users.FindByIdAsync(id.ToString()) is not { } user) return IdentityErrors.SessionExpired;
        return SessionService.ToMe(user, await users.GetRolesAsync(user));
    }
}
