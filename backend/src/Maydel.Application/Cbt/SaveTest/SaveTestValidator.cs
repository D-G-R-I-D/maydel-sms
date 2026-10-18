using FluentValidation;

namespace Maydel.Application.Cbt.SaveTest;

internal sealed class SaveTestValidator : AbstractValidator<SaveTestCommand>
{
    public SaveTestValidator()
    {
        RuleFor(x => x.Test).NotNull();
        RuleFor(x => x.Test.Title).NotEmpty().MaximumLength(150).OverridePropertyName("title");
        RuleFor(x => x.Test.Instructions).MaximumLength(2000).OverridePropertyName("instructions");
        RuleFor(x => x.Test.ClassRoomIds).Must(r => r is { Count: <= 30 }).OverridePropertyName("classRoomIds").WithMessage("Choose up to 30 classes.");
    }
}
