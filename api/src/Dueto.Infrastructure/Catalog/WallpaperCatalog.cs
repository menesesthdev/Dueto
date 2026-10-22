using Dueto.Core.Catalog;
using Dueto.Core.Domain;
using Dueto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dueto.Infrastructure.Catalog;

internal sealed class WallpaperCatalog(DuetoDbContext db) : IWallpaperCatalog
{
    public async Task<IReadOnlyList<WallpaperDto>> ListAsync(CancellationToken cancellationToken)
    {
        var wallpapers = await db.Wallpapers
            .AsNoTracking()
            .OrderByDescending(w => w.CreatedAt)
            .ThenBy(w => w.Id)
            .ToListAsync(cancellationToken);

        return wallpapers.Select(ToDto).ToList();
    }

    public async Task<WallpaperDto?> FindAsync(string id, CancellationToken cancellationToken)
    {
        var wallpaper = await db.Wallpapers
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.Id == id, cancellationToken);

        return wallpaper is null ? null : ToDto(wallpaper);
    }

    private static WallpaperDto ToDto(Wallpaper w) => new(
        w.Id,
        w.Title,
        w.Kind == WallpaperKind.Animated ? "animated" : "static",
        w.DesktopUrl,
        w.DesktopSha256,
        w.MobileUrl,
        w.MobileSha256,
        w.ThumbnailUrl,
        w.Tags,
        w.CreatedAt);
}
