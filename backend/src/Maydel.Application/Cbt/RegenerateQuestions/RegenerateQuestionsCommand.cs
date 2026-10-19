using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.RegenerateQuestions;

/// <summary>Writes a fresh version of the same request (up to 5 times), steering away from earlier versions. Uses one generation.</summary>
public sealed record RegenerateQuestionsCommand(Guid Id) : ICommand<AiGenerationDto>, INonTransactional;
