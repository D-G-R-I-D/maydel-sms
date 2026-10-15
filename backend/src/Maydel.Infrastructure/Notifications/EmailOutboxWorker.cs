using Maydel.Application.Abstractions;
using Maydel.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maydel.Infrastructure.Notifications;

/// <summary>Polls the outbox and sends due emails. Idle when no provider is configured. Single instance (one API process).</summary>
internal sealed class EmailOutboxWorker(IServiceScopeFactory scopes, IEmailSender sender, IOptions<EmailOptions> options, ILogger<EmailOutboxWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!sender.IsEnabled)
        {
            logger.LogInformation("Email provider is None: emails stay queued until Email:Provider is configured");
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollSeconds)));
        do
        {
            try
            {
                // Keep going while there's a backlog; otherwise wait for the next tick.
                while (!stoppingToken.IsCancellationRequested)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var sent = await scope.ServiceProvider.GetRequiredService<EmailDispatcher>().DispatchDueAsync(stoppingToken);
                    if (sent < EmailDispatcher.BatchSize) break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Email outbox run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
