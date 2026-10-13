using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.CreateSession;

/// <summary>Creates a school year such as "2026/2027". Terms are added separately.</summary>
public sealed record CreateSessionCommand(string Name, DateOnly StartsOn, DateOnly EndsOn) : ICommand<SessionDto>;
