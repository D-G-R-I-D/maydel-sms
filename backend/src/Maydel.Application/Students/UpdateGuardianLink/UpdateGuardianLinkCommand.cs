using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;

namespace Maydel.Application.Students.UpdateGuardianLink;

/// <summary>Changes relationship, primary contact or pick-up permission for one student–guardian link.</summary>
public sealed record UpdateGuardianLinkCommand(Guid StudentId, Guid GuardianId, GuardianRelationship Relationship, bool IsPrimaryContact, bool CanPickUp)
    : ICommand<StudentDetail>;
