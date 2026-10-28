using Dueto.Core.Domain;
using Dueto.Core.Pairing;
using Dueto.Core.Push;
using Dueto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dueto.Infrastructure.Push;

internal sealed class PushSubscriptionService(DuetoDbContext db) : IPushSubscriptionService
{
    public async Task SubscribeAsync(DeviceIdentity identity, PushTarget target, CancellationToken cancellationToken)
    {
        // O endpoint identifica um navegador. Se ele reaparece em outro dispositivo (novo pareamento),
        // a assinatura muda de dono em vez de duplicar.
        var existing = await db.PushSubscriptions
            .SingleOrDefaultAsync(s => s.Endpoint == target.Endpoint, cancellationToken);

        if (existing is null)
        {
            db.PushSubscriptions.Add(new Core.Domain.PushSubscription
            {
                Id = Guid.NewGuid(),
                DeviceId = identity.DeviceId,
                Endpoint = target.Endpoint,
                P256dh = target.P256dh,
                Auth = target.Auth,
            });
        }
        else
        {
            existing.DeviceId = identity.DeviceId;
            existing.P256dh = target.P256dh;
            existing.Auth = target.Auth;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnsubscribeAsync(DeviceIdentity identity, string endpoint, CancellationToken cancellationToken)
    {
        // Filtra pelo dispositivo: um token não remove assinatura de outro dispositivo.
        await db.PushSubscriptions
            .Where(s => s.Endpoint == endpoint && s.DeviceId == identity.DeviceId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
