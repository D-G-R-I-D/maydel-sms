using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Attendance.GetRegister;

/// <summary>A class's register for a day (default: today). Admins and the class's teachers.</summary>
public sealed record GetRegisterQuery(Guid ClassRoomId, DateOnly? Date) : IQuery<ClassRegister>;
