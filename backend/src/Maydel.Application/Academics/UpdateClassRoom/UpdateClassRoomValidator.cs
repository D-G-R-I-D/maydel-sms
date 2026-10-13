using FluentValidation;

namespace Maydel.Application.Academics.UpdateClassRoom;

internal sealed class UpdateClassRoomValidator : AbstractValidator<UpdateClassRoomCommand>
{
    public UpdateClassRoomValidator()
    {
        RuleFor(x => x.Arm).NotEmpty().WithMessage("Give the arm a name, e.g. A or Gold.").MaximumLength(32);
    }
}
