using FluentValidation;

namespace Maydel.Application.Website.SavePost;

internal sealed class SavePostValidator : AbstractValidator<SavePostCommand>
{
    public SavePostValidator()
    {
        RuleFor(x => x.Post).NotNull();
        RuleFor(x => x.Post.Summary).MaximumLength(500).OverridePropertyName("summary");
        RuleFor(x => x.Post.Body).MaximumLength(20000).OverridePropertyName("body");
        RuleFor(x => x.Post.Category).MaximumLength(60).OverridePropertyName("category");
        RuleFor(x => x.Post.LinkUrl).MaximumLength(500).OverridePropertyName("linkUrl");
        RuleFor(x => x.Post.LinkText).MaximumLength(60).OverridePropertyName("linkText");
    }
}
