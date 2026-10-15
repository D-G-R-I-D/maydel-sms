using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Access;
using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Attendance.GetRegister;

internal sealed class GetRegisterHandler(AccessRules access, RegisterReader reader, ISchoolClock clock) : IQueryHandler<GetRegisterQuery, ClassRegister>
{
    public async Task<Result<ClassRegister>> Handle(GetRegisterQuery q, CancellationToken ct)
    {
        if (!await access.TeachesClassAsync(q.ClassRoomId, ct)) return AttendanceErrors.NotAllowed;
        return await reader.ReadAsync(q.ClassRoomId, q.Date ?? clock.Today, ct) is { } register ? register : AttendanceErrors.ClassNotFound;
    }
}
