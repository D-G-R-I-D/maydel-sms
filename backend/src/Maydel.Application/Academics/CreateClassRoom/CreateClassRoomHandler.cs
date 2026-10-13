using Maydel.Application.Abstractions;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Domain.Academics;
using Microsoft.EntityFrameworkCore;

namespace Maydel.Application.Academics.CreateClassRoom;

internal sealed class CreateClassRoomHandler(IAppDbContext db) : ICommandHandler<CreateClassRoomCommand, ClassRoomDto>
{
    public async Task<Result<ClassRoomDto>> Handle(CreateClassRoomCommand c, CancellationToken ct)
    {
        var arm = c.Arm.Trim();
        if (ClassRoom.Validate(arm, c.Capacity) is { } invalid) return invalid.ToError();

        var level = await db.ClassLevels.FirstOrDefaultAsync(l => l.Id == c.ClassLevelId, ct);
        if (level is null) return Error.Validation("classLevelId", "Choose a year group.");
        if (c.FormTeacherId is { } teacherId && !await db.IsActiveStaffAsync(teacherId, ct))
            return Error.Validation("formTeacherId", "Choose an active staff member.");
        if (await db.ClassRooms.AnyAsync(r => r.ClassLevelId == level.Id && r.Arm.ToLower() == arm.ToLower(), ct))
            return AcademicsErrors.DuplicateClassRoom($"{level.Name} {arm}");

        var room = new ClassRoom { ClassLevelId = level.Id, Arm = arm, Capacity = c.Capacity, FormTeacherId = c.FormTeacherId };
        db.ClassRooms.Add(room);
        await db.SaveChangesAsync(ct);
        return await db.ClassRoomDtos(room.Id).SingleAsync(ct);
    }
}
