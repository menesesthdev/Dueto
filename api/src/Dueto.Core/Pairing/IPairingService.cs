using Dueto.Core.Domain;

namespace Dueto.Core.Pairing;

/// <param name="DeviceToken">Token do dispositivo. Entregue só uma vez; no banco fica apenas o hash.</param>
public sealed record PairingStarted(string DeviceToken, string Code, DateTimeOffset ExpiresAt);

public enum PairingCompleteStatus
{
    Success,

    /// <summary>Código inexistente, expirado ou já usado (propositalmente indistinguíveis).</summary>
    InvalidCode,

    /// <summary>O par já tem um dispositivo desse tipo.</summary>
    KindConflict,
}

public sealed record PairingCompleteResult(PairingCompleteStatus Status, string? DeviceToken = null);

public interface IPairingService
{
    /// <summary>Cria um par com o primeiro dispositivo e um código para o segundo.</summary>
    Task<PairingStarted> StartAsync(DeviceKind kind, string deviceName, CancellationToken cancellationToken);

    /// <summary>Consome o código (uso único) e adiciona o segundo dispositivo ao par.</summary>
    Task<PairingCompleteResult> CompleteAsync(
        string code,
        DeviceKind kind,
        string deviceName,
        CancellationToken cancellationToken);
}
