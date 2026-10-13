using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;

namespace Maydel.Application.Students.AddGuardian;

/// <summary>
/// Links a parent/guardian to a student (E3.2). If a guardian with the same phone exists (a sibling's parent), that guardian
/// is reused so the family shares one parent account. Marking primary contact unsets it on the student's other guardians.
/// </summary>
public sealed record AddGuardianCommand(
    Guid StudentId, string FullName, string Phone, string? Email, string? Occupation, string? Address,
    GuardianRelationship Relationship, bool IsPrimaryContact, bool CanPickUp) : ICommand<GuardianAdded>;
