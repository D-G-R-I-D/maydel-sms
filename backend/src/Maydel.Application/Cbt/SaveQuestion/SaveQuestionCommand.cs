using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Cbt.SaveQuestion;

/// <summary>Adds a question to the bank (<c>Id</c> null) or edits one. Teachers: only subjects and levels they teach.</summary>
public sealed record SaveQuestionCommand(Guid? Id, QuestionInput Question) : ICommand<QuestionDto>;
