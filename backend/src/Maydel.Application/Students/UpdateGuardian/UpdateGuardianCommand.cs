using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Students.UpdateGuardian;

/// <summary>Edits a guardian's details (shared by all their children). Phone stays unique across guardians.</summary>
public sealed record UpdateGuardianCommand(Guid Id, string FullName, string Phone, string? Email, string? Occupation, string? Address)
    : ICommand<GuardianDetail>;
