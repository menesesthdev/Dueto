namespace Dueto.Core.Domain;

/// <summary>
/// Última confirmação de um dispositivo: qual wallpaper e qual versão ele
/// aplicou de fato na tela. Uma linha por dispositivo (o último aplicado).
/// </summary>
public class DeviceApplied
{
    public Guid DeviceId { get; set; }

    public required string WallpaperId { get; set; }

    public long Version { get; set; }

    public DateTimeOffset AppliedAt { get; set; }
}
