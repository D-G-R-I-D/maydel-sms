using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Attendance.SaveRegister;

/// <summary>
/// Saves a class's register for a day (E9.1). Active students left out are recorded Present, so one tap ("all present")
/// takes the register. Form teacher: today only; admin: any earlier school day. Students whose term report is published
/// can't change (422). One record per student per day.
/// </summary>
public sealed record SaveRegisterCommand(Guid ClassRoomId, DateOnly Date, IReadOnlyList<RegisterEntryInput> Entries) : ICommand<ClassRegister>;
