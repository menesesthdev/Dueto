namespace Dueto.Core.Push;

public interface IPushDispatcher
{
    /// <summary>
    /// Envia o push a todas as assinaturas do dispositivo e remove as que o push service
    /// declarou mortas. Devolve quantos envios deram certo.
    /// </summary>
    Task<int> SendToDeviceAsync(Guid deviceId, PushPayload payload, CancellationToken cancellationToken);
}
