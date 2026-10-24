using Maydel.Application.Common.Messaging;
using Maydel.Application.Finance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maydel.Infrastructure.Finance;

/// <summary>Sends overdue-fee reminders (E12.8). Checks hourly; the sweep itself only acts in office hours and at most once per interval.</summary>
internal sealed class FeeRemindersWorker(IServiceScopeFactory scopes, ILogger<FeeRemindersWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sweep = scope.ServiceProvider.GetRequiredService<ICommandHandler<SendFeeRemindersCommand, FeeReminderResult>>();
                var result = await sweep.Handle(new SendFeeRemindersCommand(), stoppingToken);
                if (result.IsSuccess && result.Value.Invoices > 0)
                    logger.LogInformation("Fee reminders: {Invoices} invoices, {Emails} emails", result.Value.Invoices, result.Value.Emails);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Fee reminder sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
