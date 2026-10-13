using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Students.RemoveGuardian;

/// <summary>Unlinks a guardian from one student. The guardian record (and any login) stays for their other children.</summary>
public sealed record RemoveGuardianCommand(Guid StudentId, Guid GuardianId) : ICommand;
