using Dueto.Core.Pairing;

namespace Dueto.Core.Push;

public interface IPushSubscriptionService
{
    /// <summary>Registra (ou atualiza) a assinatura do dispositivo. O endpoint é único no sistema.</summary>
    Task SubscribeAsync(DeviceIdentity identity, PushTarget target, CancellationToken cancellationToken);

    /// <summary>Remove a assinatura do dispositivo. Idempotente.</summary>
    Task UnsubscribeAsync(DeviceIdentity identity, string endpoint, CancellationToken cancellationToken);
}
