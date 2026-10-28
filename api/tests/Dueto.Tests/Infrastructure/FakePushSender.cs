using System.Collections.Concurrent;
using Dueto.Core.Push;

namespace Dueto.Tests.Infrastructure;

/// <summary>Remetente falso: registra o que seria enviado, sem rede. O CI nunca fala com um push service.</summary>
public sealed class FakePushSender : IPushSender
{
    private readonly ConcurrentQueue<(PushTarget Target, PushPayload Payload)> _sent = new();

    public bool IsEnabled { get; set; } = true;

    public string? VapidPublicKey => IsEnabled ? "test-vapid-public-key" : null;

    /// <summary>Resposta devolvida para cada envio. Pode lançar exceção para simular falha grave.</summary>
    public Func<PushTarget, PushSendStatus> Responder { get; set; } = _ => PushSendStatus.Sent;

    public Task<PushSendStatus> SendAsync(PushTarget target, PushPayload payload, CancellationToken cancellationToken)
    {
        _sent.Enqueue((target, payload));
        return Task.FromResult(Responder(target));
    }

    public IReadOnlyList<PushPayload> SentTo(string endpoint) =>
        _sent.Where(s => s.Target.Endpoint == endpoint).Select(s => s.Payload).ToList();

    public void Reset()
    {
        IsEnabled = true;
        Responder = _ => PushSendStatus.Sent;
    }
}
