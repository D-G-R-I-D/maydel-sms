using FluentValidation;

namespace Maydel.Application.Cbt.SaveQuestion;

internal sealed class SaveQuestionValidator : AbstractValidator<SaveQuestionCommand>
{
    public SaveQuestionValidator()
    {
        RuleFor(x => x.Question).NotNull();
        RuleFor(x => x.Question.SubjectId).NotEmpty().OverridePropertyName("subjectId");
        RuleFor(x => x.Question.ClassLevelId).NotEmpty().OverridePropertyName("classLevelId");
        RuleFor(x => x.Question.Kind).IsInEnum().OverridePropertyName("kind");
        RuleFor(x => x.Question.Difficulty).IsInEnum().OverridePropertyName("difficulty");
        RuleFor(x => x.Question.Options).Must(o => o is null || o.Count <= Domain.Cbt.Question.MaxOptions).OverridePropertyName("options")
            .WithMessage($"Give at most {Domain.Cbt.Question.MaxOptions} options.");
    }
}
