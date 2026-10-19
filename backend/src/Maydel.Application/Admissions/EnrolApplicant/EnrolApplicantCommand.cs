using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.EnrolApplicant;

/// <summary>
/// Enrols an accepted applicant (admin, E11.2/E11.8): creates the student with the reserved admission number and the form's
/// details, links the parents as guardians (reusing existing guardians by phone, so siblings join up), turns the family's
/// applicant login into their parent login, and emails them. New-intake fees are billed when the bursar generates invoices
/// for the term (E10.6: the admission date marks the child as new intake).
/// </summary>
public sealed record EnrolApplicantCommand(Guid ApplicationId, Guid? ClassRoomId, Guid? EntryTermId = null) : ICommand<EnrolmentResult>;

public enum ParentLoginOutcome
{
    /// <summary>The family's login is now their parent login and shows the child.</summary>
    Linked,
    /// <summary>The applicant's phone belongs to a guardian already linked to a different login; link the family by hand.</summary>
    PhoneBelongsToAnotherAccount,
    /// <summary>No guardian with a usable phone number could be made from the form.</summary>
    NoGuardian,
}

/// <summary><c>Message</c>: for the admin when the parent login couldn't be linked automatically.</summary>
public sealed record EnrolmentResult(Guid StudentId, string AdmissionNumber, ApplicationDetail Application, ParentLoginOutcome ParentLogin, string? Message);
