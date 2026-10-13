using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;

namespace Maydel.Application.Students.IssueStudentLogin;

/// <summary>
/// Creates the student's login (username = admission number) or resets it (E3.3). Returns a temporary password once;
/// it can never be retrieved later. A reset signs the student out everywhere.
/// </summary>
public sealed record IssueStudentLoginCommand(Guid Id) : ICommand<IssuedCredentials>;
