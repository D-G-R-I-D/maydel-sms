using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Academics.CreateSubject;

/// <summary>Adds a subject to the catalogue. Codes are stored upper-case; name and code are unique.</summary>
public sealed record CreateSubjectCommand(string Name, string Code, bool IsActive = true) : ICommand<SubjectDto>;
