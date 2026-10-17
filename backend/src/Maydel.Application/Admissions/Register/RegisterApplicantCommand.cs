using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Admissions.Register;

/// <summary>
/// "Buy admission form" (E11.4): creates the family's applicant account (the same login becomes their parent account
/// if a child is enrolled) and the application, awaiting payment for the form. <c>Website</c> is a honeypot: humans
/// leave it empty. Families who already have an account sign in and use <c>StartApplication</c> instead.
/// </summary>
public sealed record RegisterApplicantCommand(
    string ParentName, string Email, string Phone, string Password, string ChildFirstName, string ChildLastName, DateOnly? ChildDateOfBirth,
    Guid ClassLevelId, Guid SessionId, string? Website = null) : ICommand<ApplicationDto>;
