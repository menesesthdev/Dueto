namespace Dueto.Core.Domain;

/// <summary>
/// Estado atual desejado de um par: qual wallpaper e qual versão. A
/// <see cref="Version"/> é um contador crescente: quem já aplicou uma versão
/// maior ou igual ignora eventos antigos.
/// </summary>
public class PairState
{
    public Guid PairId { get; set; }

    /// <summary>Wallpaper desejado. Nulo enquanto o par nunca definiu nenhum.</summary>
    public string? WallpaperId { get; set; }

    /// <summary>Contador crescente. Todo PUT de estado incrementa.</summary>
    public long Version { get; set; }

    /// <summary>Dispositivo que fez a troca mais recente (para evitar eco).</summary>
    public Guid? OriginDeviceId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
