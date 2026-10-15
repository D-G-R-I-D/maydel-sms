using Maydel.Domain.Communication;

namespace Maydel.Application.Notifications;

/// <summary>Delivery status of one report email (E12.3 "delivery status visible to admin").</summary>
public sealed record ReportEmailStatus(
    Guid Id, Guid StudentId, string Student, string Guardian, string ToAddress, EmailStatus Status, int Attempts,
    string? LastError, DateTimeOffset QueuedAt, DateTimeOffset? SentAt);

public static class NotificationErrors
{
    public static readonly Common.Error EmailNotFound = Common.Error.NotFound("email.not_found", "That email doesn't exist.");
}
