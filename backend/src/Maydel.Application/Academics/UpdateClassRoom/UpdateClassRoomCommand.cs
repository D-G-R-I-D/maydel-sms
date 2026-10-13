using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.UpdateClassRoom;

/// <summary>Renames the arm, changes capacity, or (re)assigns the form teacher (E4.2). The year group can't change.</summary>
public sealed record UpdateClassRoomCommand(Guid Id, string Arm, int Capacity, Guid? FormTeacherId) : ICommand<ClassRoomDto>;
