namespace Dueto.Core.Catalog;

/// <summary>Contrato público de um wallpaper do catálogo (<c>GET /api/wallpapers</c>).</summary>
/// <param name="Kind">"static" ou "animated".</param>
/// <param name="MobileUrl">Nulo em wallpapers animados (só desktop).</param>
public sealed record WallpaperDto(
    string Id,
    string Title,
    string Kind,
    string DesktopUrl,
    string DesktopSha256,
    string? MobileUrl,
    string? MobileSha256,
    string ThumbnailUrl,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt);
