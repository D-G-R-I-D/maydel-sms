using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Academics;
using Maydel.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.UpdateClassRoom;

internal sealed class UpdateClassRoomHandler(IAppDbContext db) : ICommandHandler<UpdateClassRoomCommand, ClassRoomDto>
{
    public async Task<Result<ClassRoomDto>> Handle(UpdateClassRoomCommand c, CancellationToken ct)
    {
        var room = await db.ClassRooms.Include(r => r.ClassLevel).FirstOrDefaultAsync(r => r.Id == c.Id, ct);
        if (room is null) return AcademicsErrors.ClassRoomNotFound;

        var arm = c.Arm.Trim();
        var enrolled = await db.Students.CountAsync(s => s.ClassRoomId == room.Id && s.Status == StudentStatus.Active, ct);
        if (ClassRoom.Validate(arm, c.Capacity, enrolled) is { } invalid) return invalid.ToError();
        if (c.FormTeacherId is { } teacherId && teacherId != room.FormTeacherId && !await db.IsActiveStaffAsync(teacherId, ct))
            return Error.Validation("formTeacherId", "Choose an active staff member.");
        if (await db.ClassRooms.AnyAsync(r => r.Id != room.Id && r.ClassLevelId == room.ClassLevelId && r.Arm.ToLower() == arm.ToLower(), ct))
            return AcademicsErrors.DuplicateClassRoom($"{room.ClassLevel.Name} {arm}");

        (room.Arm, room.Capacity, room.FormTeacherId) = (arm, c.Capacity, c.FormTeacherId);
        await db.SaveChangesAsync(ct);
        return await db.ClassRoomDtos(room.Id).SingleAsync(ct);
    }
}
