using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.CreateClassRoom;

/// <summary>Adds an arm to a year group (e.g. Year 3 B), optionally with its form teacher.</summary>
public sealed record CreateClassRoomCommand(Guid ClassLevelId, string Arm, int Capacity, Guid? FormTeacherId) : ICommand<ClassRoomDto>;
