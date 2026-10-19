using Maydel.Application.Common.Messaging;
using Maydel.Domain.Cbt;

namespace Maydel.Application.Cbt.GenerateQuestions;

/// <summary>An uploaded file as received (re-encoded or checked by the handler).</summary>
public sealed record UploadedFile(string Name, byte[] Content);

/// <summary>
/// Asks the AI to write draft questions from the teacher's material (E16.8). Uses one of the teacher's monthly generations
/// when it succeeds (E16.9). Teachers: only for subjects and levels they teach.
/// </summary>
public sealed record GenerateQuestionsCommand(
    Guid SubjectId, Guid ClassLevelId, Guid? TermId, AssessmentType AssessmentType, IReadOnlyList<QuestionKind> Kinds, int Count, int Easy, int Medium,
    int Hard, string? Guidance, IReadOnlyList<UploadedFile> Files) : ICommand<AiGenerationDto>, Common.Messaging.INonTransactional;
