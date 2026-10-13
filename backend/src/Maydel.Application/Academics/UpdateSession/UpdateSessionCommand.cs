using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.UpdateSession;

/// <summary>Renames or re-dates a session. Its terms must still fit inside the new dates.</summary>
public sealed record UpdateSessionCommand(Guid Id, string Name, DateOnly StartsOn, DateOnly EndsOn) : ICommand<SessionDto>;
