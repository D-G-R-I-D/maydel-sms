using FluentValidation;

namespace Maydel.Application.Academics.CreateClassRoom;

internal sealed class CreateClassRoomValidator : AbstractValidator<CreateClassRoomCommand>
{
    public CreateClassRoomValidator()
    {
        RuleFor(x => x.Arm).NotEmpty().WithMessage("Give the arm a name, e.g. A or Gold.").MaximumLength(32);
    }
}
