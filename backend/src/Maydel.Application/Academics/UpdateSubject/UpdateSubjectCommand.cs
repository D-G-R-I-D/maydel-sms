using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.UpdateSubject;

/// <summary>Renames a subject or (de)activates it. Inactive subjects stay on existing classes and reports but can't be newly offered.</summary>
public sealed record UpdateSubjectCommand(Guid Id, string Name, string Code, bool IsActive) : ICommand<SubjectDto>;
