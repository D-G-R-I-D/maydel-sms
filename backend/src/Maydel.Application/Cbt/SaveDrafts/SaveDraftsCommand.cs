using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.SaveDrafts;

/// <summary>
/// Saves the chosen (and edited) questions from an AI request to the bank, marked as AI-written (E16.8). Every question is
/// checked like any other; nothing is saved if one is invalid.
/// </summary>
public sealed record SaveDraftsCommand(Guid GenerationId, SaveDraftsInput Drafts) : ICommand<IReadOnlyList<QuestionDto>>;
