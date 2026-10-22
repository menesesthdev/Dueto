namespace Dueto.Core.Domain;

/// <summary>
/// Um wallpaper do catálogo. O <see cref="Id"/> é um slug estável (ex.: "aurora-01").
/// Os arquivos vivem em GitHub Releases; aqui guardamos só URL e checksum.
/// </summary>
public class Wallpaper
{
    /// <summary>Slug do wallpaper, usado como id e em <c>dueto://apply?id=</c>.</summary>
    public required string Id { get; set; }

    public required string Title { get; set; }

    public WallpaperKind Kind { get; set; }

    /// <summary>URL do arquivo para desktop. Sempre presente.</summary>
    public required string DesktopUrl { get; set; }

    public required string DesktopSha256 { get; set; }

    /// <summary>URL do arquivo para mobile. Nula em animados (só desktop).</summary>
    public string? MobileUrl { get; set; }

    public string? MobileSha256 { get; set; }

    public required string ThumbnailUrl { get; set; }

    public string[] Tags { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}
