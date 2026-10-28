namespace Dueto.Core.Push;

/// <summary>Para onde enviar: os dados da assinatura Web Push de um navegador.</summary>
public sealed record PushTarget(string Endpoint, string P256dh, string Auth);

/// <summary>Conteúdo mínimo do push (sem dado sensível): só o que o PWA precisa para agir.</summary>
public sealed record PushPayload(string WallpaperId, long Version);

public enum PushSendStatus
{
    Sent,

    /// <summary>O push service respondeu 404/410: a assinatura não existe mais e deve ser removida.</summary>
    SubscriptionGone,

    /// <summary>Falha transitória (rede, 5xx, limite). A assinatura continua válida.</summary>
    Failed,
}

public interface IPushSender
{
    /// <summary>Falso quando as chaves VAPID não foram configuradas: o recurso inteiro fica desligado.</summary>
    bool IsEnabled { get; }

    string? VapidPublicKey { get; }

    Task<PushSendStatus> SendAsync(PushTarget target, PushPayload payload, CancellationToken cancellationToken);
}
