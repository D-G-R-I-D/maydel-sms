using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.ArchiveQuestion;

/// <summary>Removes a question from the bank. It's archived, not erased, so tests that used it keep showing it.</summary>
public sealed record ArchiveQuestionCommand(Guid Id) : ICommand;
