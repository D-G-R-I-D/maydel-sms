using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.ListClassRooms;

/// <summary>Every class (level + arm) with form teacher, capacity and active student count, in level order.</summary>
public sealed record ListClassRoomsQuery : IQuery<IReadOnlyList<ClassRoomDto>>;
