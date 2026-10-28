using Dueto.Core.Domain;
using Dueto.Core.Push;
using Dueto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dueto.Infrastructure.Push;

internal sealed class ReminderProcessor(
    DuetoDbContext db,
    TimeProvider timeProvider,
    IPushSender sender,
    IPushDispatcher dispatcher,
    ILogger<ReminderProcessor> logger) : IReminderProcessor
{
    private const int BatchSize = 50;

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        if (!sender.IsEnabled)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();

        var due = await db.Reminders
            .AsNoTracking()
            .Where(r => r.Status == ReminderStatus.Pending && r.DueAt <= now)
            .OrderBy(r => r.DueAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var claimedCount = 0;
        foreach (var reminder in due)
        {
            // Reivindicação atômica: só quem consegue mudar Pending para Sent envia. Se o processo
            // morrer entre marcar e enviar, perde-se um lembrete, nunca se duplica um.
            var claimed = await db.Reminders
                .Where(r => r.Id == reminder.Id && r.Status == ReminderStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReminderStatus.Sent), cancellationToken);

            if (claimed == 0)
            {
                continue;
            }

            claimedCount++;
            await SendAsync(reminder, cancellationToken);
        }

        return claimedCount;
    }

    private async Task SendAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        // O lembrete só guarda a versão. O wallpaper vem do estado atual do par, e só vale se a
        // versão ainda for a vigente (uma troca mais nova já teria cancelado este lembrete).
        var current = await (
            from device in db.Devices.AsNoTracking()
            join state in db.PairStates.AsNoTracking() on device.PairId equals state.PairId
            where device.Id == reminder.DeviceId
            select new { state.WallpaperId, state.Version })
            .SingleOrDefaultAsync(cancellationToken);

        if (current?.WallpaperId is null || current.Version != reminder.Version)
        {
            return;
        }

        try
        {
            await dispatcher.SendToDeviceAsync(
                reminder.DeviceId,
                new PushPayload(current.WallpaperId, current.Version),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Failed to send reminder {Sequence} for version {Version}.", reminder.Sequence, reminder.Version);
        }
    }
}
