using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Students.IssueGuardianLogin;

/// <summary>
/// Creates or resets a parent login (E3.3). Username = email, or the phone (+234…) when there is no email. If that email
/// already has a non-student account (e.g. a teacher who is also a parent), it is linked and given the Parent role instead.
/// One login covers all the guardian's children.
/// </summary>
public sealed record IssueGuardianLoginCommand(Guid Id) : ICommand<GuardianLoginIssued>;
