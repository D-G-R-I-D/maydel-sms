using Maydel.Application.Common;

namespace Maydel.Application.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_the_value()
    {
        Result<int> result = 42;
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_throws()
    {
        Result<int> result = Error.NotFound();
        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Validation_helper_builds_a_single_field_error()
    {
        var error = Error.Validation("email", "Required.");
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(["Required."], error.Errors["email"]);
    }
}
