namespace Dueto.Core.Domain;

/// <summary>
/// Um dispositivo pareado. O token de autenticação nunca é guardado em claro:
/// só o seu hash fica em <see cref="TokenHash"/>.
/// </summary>
public class Device
{
    public Guid Id { get; set; }

    public Guid PairId { get; set; }

    public DeviceKind Kind { get; set; }

    public required string Name { get; set; }

    /// <summary>Hash do token do dispositivo (nunca o token em claro).</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }
}
