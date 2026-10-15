using Maydel.Domain.Communication;

namespace Maydel.Domain.Tests.Communication;

public sealed class OutboxEmailTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    private static OutboxEmail Email() => new() { ToAddress = "parent@example.com", Subject = "s", Body = "b", NextAttemptAt = Now };

    [Fact]
    public void Failures_back_off_then_give_up_after_five_attempts()
    {
        var email = Email();
        var waits = new List<TimeSpan>();
        for (var i = 0; i < OutboxEmail.MaxAttempts - 1; i++)
        {
            email.MarkFailed("smtp down", Now);
            waits.Add(email.NextAttemptAt - Now);
            Assert.Equal(EmailStatus.Pending, email.Status);
        }
        Assert.Equal([1, 2, 4, 8], waits.Select(w => w.TotalMinutes));

        email.MarkFailed("smtp down", Now);
        Assert.Equal((EmailStatus.Failed, 5), (email.Status, email.Attempts));
    }

    [Fact]
    public void Long_errors_are_truncated_and_success_clears_them()
    {
        var email = Email();
        email.MarkFailed(new string('x', 2000), Now);
        Assert.Equal(500, email.LastError!.Length);

        email.MarkSent(Now);
        Assert.Equal((EmailStatus.Sent, (string?)null, (DateTimeOffset?)Now), (email.Status, email.LastError, email.SentAt));
    }

    [Fact]
    public void Only_failed_emails_can_be_retried()
    {
        var email = Email();
        Assert.Equal("email.not_failed", email.Retry(Now)?.Code);
        for (var i = 0; i < OutboxEmail.MaxAttempts; i++) email.MarkFailed("x", Now);

        Assert.Null(email.Retry(Now.AddHours(1)));
        Assert.Equal((EmailStatus.Pending, 0, Now.AddHours(1)), (email.Status, email.Attempts, email.NextAttemptAt));
    }
}
