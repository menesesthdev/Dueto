using System.Text.RegularExpressions;
using Dueto.Core.Domain;

namespace Dueto.Core.Catalog;

public sealed record CatalogValidationResult(IReadOnlyList<Wallpaper> Wallpapers, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Valida o catálogo inteiro antes de qualquer escrita: um erro em qualquer entrada impede toda a importação.
/// O SHA-256 e as URLs são o que o desktop vai usar para baixar e conferir o arquivo, então um valor errado aqui
/// só apareceria em runtime, no aparelho do usuário.
/// </summary>
public static partial class CatalogValidator
{
    public const int MaxWallpapers = 1_000;
    public const int MaxTitleLength = 200;
    public const int MaxTags = 20;

    [GeneratedRegex(@"^[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    [GeneratedRegex(@"^[a-z0-9-]{1,32}\z", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    public static CatalogValidationResult Validate(CatalogDocument? document, IReadOnlyList<string> allowedHosts)
    {
        var errors = new List<string>();
        var wallpapers = new List<Wallpaper>();

        if (document?.Wallpapers is null)
        {
            errors.Add("The catalog must have a \"wallpapers\" array.");
            return new CatalogValidationResult(wallpapers, errors);
        }

        if (document.Wallpapers.Count > MaxWallpapers)
        {
            errors.Add($"The catalog has more than {MaxWallpapers} wallpapers.");
            return new CatalogValidationResult(wallpapers, errors);
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < document.Wallpapers.Count; index++)
        {
            var entry = document.Wallpapers[index];
            var path = $"wallpapers[{index}]";

            if (entry is null)
            {
                errors.Add($"{path}: the entry is empty.");
                continue;
            }

            var before = errors.Count;
            var wallpaper = ValidateEntry(entry, path, allowedHosts, seenIds, errors);
            if (errors.Count == before && wallpaper is not null)
            {
                wallpapers.Add(wallpaper);
            }
        }

        return new CatalogValidationResult(wallpapers, errors);
    }

    private static Wallpaper? ValidateEntry(
        CatalogEntry entry,
        string path,
        IReadOnlyList<string> allowedHosts,
        HashSet<string> seenIds,
        List<string> errors)
    {
        var errorsBefore = errors.Count;

        if (!WallpaperId.IsValid(entry.Id))
        {
            errors.Add($"{path}.id: must match ^[a-z0-9-]{{1,{WallpaperId.MaxLength}}}$.");
        }
        else if (!seenIds.Add(entry.Id!))
        {
            errors.Add($"{path}.id: \"{entry.Id}\" is duplicated.");
        }

        var title = entry.Title?.Trim() ?? string.Empty;
        if (title.Length is 0 or > MaxTitleLength)
        {
            errors.Add($"{path}.title: must have between 1 and {MaxTitleLength} characters.");
        }

        var kind = ParseKind(entry.Kind);
        if (kind is null)
        {
            errors.Add($"{path}.kind: must be \"static\" or \"animated\".");
        }

        CheckUrl(entry.DesktopUrl, $"{path}.desktopUrl", allowedHosts, errors);
        CheckSha256(entry.DesktopSha256, $"{path}.desktopSha256", errors);
        CheckUrl(entry.ThumbnailUrl, $"{path}.thumbnailUrl", allowedHosts, errors);

        var hasMobileUrl = !string.IsNullOrWhiteSpace(entry.MobileUrl);
        var hasMobileSha = !string.IsNullOrWhiteSpace(entry.MobileSha256);
        if (hasMobileUrl)
        {
            CheckUrl(entry.MobileUrl, $"{path}.mobileUrl", allowedHosts, errors);
        }

        if (hasMobileSha)
        {
            CheckSha256(entry.MobileSha256, $"{path}.mobileSha256", errors);
        }

        if (hasMobileUrl != hasMobileSha)
        {
            errors.Add($"{path}: mobileUrl and mobileSha256 must be set together.");
        }

        // Animado é só do desktop; estático precisa poder ser aplicado no celular.
        if (kind == WallpaperKind.Animated && (hasMobileUrl || hasMobileSha))
        {
            errors.Add($"{path}: an animated wallpaper is desktop-only and must not have mobileUrl or mobileSha256.");
        }

        if (kind == WallpaperKind.Static && !(hasMobileUrl && hasMobileSha))
        {
            errors.Add($"{path}: a static wallpaper needs mobileUrl and mobileSha256.");
        }

        var tags = entry.Tags ?? [];
        if (tags.Count > MaxTags || tags.Any(t => t is null || !TagPattern().IsMatch(t)))
        {
            errors.Add($"{path}.tags: at most {MaxTags} tags, each matching ^[a-z0-9-]{{1,32}}$.");
        }

        if (entry.CreatedAt is null)
        {
            errors.Add($"{path}.createdAt: is required (ISO 8601).");
        }

        // Com qualquer erro nesta entrada (inclusive campos nulos) não se monta o Wallpaper: só se reporta.
        if (errors.Count > errorsBefore || kind is null || entry.CreatedAt is null)
        {
            return null;
        }

        return new Wallpaper
        {
            Id = entry.Id!,
            Title = title,
            Kind = kind.Value,
            DesktopUrl = entry.DesktopUrl!,
            DesktopSha256 = entry.DesktopSha256!.ToLowerInvariant(),
            MobileUrl = hasMobileUrl ? entry.MobileUrl : null,
            MobileSha256 = hasMobileSha ? entry.MobileSha256!.ToLowerInvariant() : null,
            ThumbnailUrl = entry.ThumbnailUrl!,
            Tags = [.. tags],
            CreatedAt = entry.CreatedAt.Value.ToUniversalTime(),
        };
    }

    private static WallpaperKind? ParseKind(string? value) => value?.ToLowerInvariant() switch
    {
        "static" => WallpaperKind.Static,
        "animated" => WallpaperKind.Animated,
        _ => null,
    };

    private static void CheckUrl(string? url, string field, IReadOnlyList<string> allowedHosts, List<string> errors)
    {
        if (!UrlAllowlist.IsAllowedHttps(url, allowedHosts))
        {
            errors.Add($"{field}: must be an https URL of an allowed host ({string.Join(", ", allowedHosts)}).");
        }
    }

    private static void CheckSha256(string? value, string field, List<string> errors)
    {
        if (value is null || !Sha256Pattern().IsMatch(value))
        {
            errors.Add($"{field}: must be 64 hexadecimal characters.");
        }
    }
}
