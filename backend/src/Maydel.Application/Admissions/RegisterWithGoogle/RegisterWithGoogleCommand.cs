using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;

namespace Maydel.Application.Admissions.RegisterWithGoogle;

/// <summary>
/// "Buy admission form" with Google (E11.4): the verified Google email becomes the applicant account (no password to remember),
/// signed in straight away. It becomes the parent login on enrolment, and Google sign-in keeps working. An email that already
/// has an account gets 409: sign in (with Google or the password) and apply from the account.
/// </summary>
public sealed record RegisterWithGoogleCommand(
    string IdToken, string ParentName, string Phone, string ChildFirstName, string ChildLastName, DateOnly? ChildDateOfBirth, Guid ClassLevelId,
    Guid SessionId, string? Website = null) : ICommand<GoogleRegistration>;

public sealed record GoogleRegistration(AuthResponse Session, ApplicationDto Application);
