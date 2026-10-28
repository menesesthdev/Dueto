namespace Dueto.Core.Catalog;

/// <summary>Formato do <c>catalog.json</c>: <c>{ "wallpapers": [ ... ] }</c>.</summary>
public sealed record CatalogDocument(IReadOnlyList<CatalogEntry>? Wallpapers);

/// <summary>Uma entrada do arquivo, ainda sem validação (todos os campos podem vir ausentes).</summary>
public sealed record CatalogEntry(
    string? Id,
    string? Title,
    string? Kind,
    string? DesktopUrl,
    string? DesktopSha256,
    string? MobileUrl,
    string? MobileSha256,
    string? ThumbnailUrl,
    IReadOnlyList<string>? Tags,
    DateTimeOffset? CreatedAt);
