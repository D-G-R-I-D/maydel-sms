using Maydel.Application.Admissions;
using Maydel.Application.Admissions.SweepAdmissions;
using Maydel.Application.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maydel.Infrastructure.Admissions;

/// <summary>Runs the admissions sweep (missed windows, exam reminders) every 15 minutes. Single instance (one API process).</summary>
internal sealed class AdmissionsWorker(IServiceScopeFactory scopes, ILogger<AdmissionsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sweep = scope.ServiceProvider.GetRequiredService<ICommandHandler<SweepAdmissionsCommand, SweepResult>>();
                var result = await sweep.Handle(new SweepAdmissionsCommand(), stoppingToken);
                if (result.IsSuccess && (result.Value.Missed > 0 || result.Value.Reminders > 0))
                    logger.LogInformation("Admissions sweep: {Missed} missed, {Reminders} reminders", result.Value.Missed, result.Value.Reminders);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Admissions sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
