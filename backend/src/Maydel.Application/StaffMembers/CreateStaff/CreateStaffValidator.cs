using FluentValidation;

namespace Maydel.Application.StaffMembers.CreateStaff;

internal sealed class CreateStaffValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffValidator()
    {
        RuleFor(x => (x.FullName ?? "").Trim()).NotEmpty().MaximumLength(150).OverridePropertyName("fullName");
        RuleFor(x => (x.Email ?? "").Trim()).NotEmpty().MaximumLength(256).EmailAddress().OverridePropertyName("email");
        RuleFor(x => x.Phone).MaximumLength(32).Matches(@"^\+?[0-9 ()-]{7,32}$").When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Enter a phone number, e.g. +234 803 000 0000.");
        RuleFor(x => x.JobTitle).MaximumLength(100);
        RuleFor(x => x.Roles).Cascade(CascadeMode.Stop).NotEmpty().WithMessage("Choose at least one role.")
            .Must(r => r.All(StaffRoles.IsStaffRole)).WithMessage("Staff roles are Admin, Teacher and Bursar.");
    }
}
