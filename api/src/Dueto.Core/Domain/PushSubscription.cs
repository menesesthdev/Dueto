namespace Dueto.Core.Domain;

/// <summary>
/// Assinatura Web Push de um dispositivo mobile. Guarda só o necessário para
/// enviar a notificação; nada de dado pessoal.
/// </summary>
public class PushSubscription
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    /// <summary>Endpoint do push service. Único por assinatura.</summary>
    public required string Endpoint { get; set; }

    public required string P256dh { get; set; }

    public required string Auth { get; set; }
}
