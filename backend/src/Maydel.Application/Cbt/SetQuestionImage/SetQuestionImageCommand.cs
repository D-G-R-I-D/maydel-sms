using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.SetQuestionImage;

/// <summary>Attaches (or replaces) the question's picture: re-encoded, up to 1200 px. <c>File</c> null removes it.</summary>
public sealed record SetQuestionImageCommand(Guid Id, byte[]? File) : ICommand<QuestionDto>;
