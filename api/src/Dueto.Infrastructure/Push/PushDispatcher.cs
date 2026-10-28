using Dueto.Core.Push;
using Dueto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dueto.Infrastructure.Push;

internal sealed class PushDispatcher(DuetoDbContext db, IPushSender sender) : IPushDispatcher
{
    public async Task<int> SendToDeviceAsync(Guid deviceId, PushPayload payload, CancellationToken cancellationToken)
    {
        if (!sender.IsEnabled)
        {
            return 0;
        }

        var subscriptions = await db.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var subscription in subscriptions)
        {
            var status = await sender.SendAsync(
                new PushTarget(subscription.Endpoint, subscription.P256dh, subscription.Auth),
                payload,
                cancellationToken);

            if (status == PushSendStatus.Sent)
            {
                sent++;
            }
            else if (status == PushSendStatus.SubscriptionGone)
            {
                await db.PushSubscriptions
                    .Where(s => s.Id == subscription.Id)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        return sent;
    }
}
