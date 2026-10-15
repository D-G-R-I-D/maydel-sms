using System.Collections.Concurrent;
using Maydel.Application.Abstractions;

namespace Maydel.Api.Tests.Infrastructure;

/// <summary>Captures sent emails. Addresses in <see cref="FailFor"/> throw, to exercise retries.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();
    public ConcurrentDictionary<string, bool> FailFor { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled => true;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (FailFor.ContainsKey(message.To)) throw new InvalidOperationException("Mailbox unavailable");
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
