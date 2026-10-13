using Maydel.Application.Common.Messaging;
using Maydel.Domain.People;

namespace Maydel.Application.Students.ChangeStudentStatus;

/// <summary>
/// Suspend, withdraw, graduate or re-admit (E3.5). Leaving Active disables the student's own login at once and removes
/// them from their parents' portal; records and history are kept. Re-activating re-enables the login.
/// </summary>
public sealed record ChangeStudentStatusCommand(Guid Id, StudentStatus Status) : ICommand<StudentDetail>;
