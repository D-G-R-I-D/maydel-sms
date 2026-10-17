using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.EnrolApplicant;

/// <summary>
/// Enrols an accepted applicant (admin, E11.2/E11.8): creates the student with the reserved admission number and the form's
/// details, links the parents as guardians (reusing existing guardians by phone, so siblings join up), turns the family's
/// applicant login into their parent login, and emails them. New-intake fees are billed when the bursar generates invoices
/// for the term (E10.6: the admission date marks the child as new intake).
/// </summary>
public sealed record EnrolApplicantCommand(Guid ApplicationId, Guid? ClassRoomId) : ICommand<EnrolmentResult>;

public sealed record EnrolmentResult(Guid StudentId, string AdmissionNumber, ApplicationDetail Application);
