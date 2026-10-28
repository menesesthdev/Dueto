using Dueto.Core.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dueto.Infrastructure.Push;

/// <summary>
/// Varre a tabela <c>reminders</c> em intervalo curto e dispara os vencidos. O estado dos lembretes
/// vive no banco, então um reinício do container não perde nada: o que venceu enquanto o serviço
/// estava parado é disparado na primeira varredura.
/// </summary>
internal sealed class ReminderScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<PushOptions> options,
    TimeProvider timeProvider,
    ILogger<ReminderScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.SchedulerIntervalSeconds);
        using var timer = new PeriodicTimer(interval, timeProvider);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IReminderProcessor>();
                await processor.ProcessDueAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Uma varredura com erro não pode derrubar o agendador: a próxima tenta de novo.
                logger.LogError(exception, "Reminder scan failed.");
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
