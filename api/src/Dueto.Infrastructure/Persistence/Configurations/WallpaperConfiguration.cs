using Dueto.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dueto.Infrastructure.Persistence.Configurations;

internal sealed class WallpaperConfiguration : IEntityTypeConfiguration<Wallpaper>
{
    public void Configure(EntityTypeBuilder<Wallpaper> builder)
    {
        builder.ToTable("wallpapers");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id").HasMaxLength(64);
        builder.Property(w => w.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(w => w.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(16);
        builder.Property(w => w.DesktopUrl).HasColumnName("desktop_url");
        builder.Property(w => w.DesktopSha256).HasColumnName("desktop_sha256").HasMaxLength(64);
        builder.Property(w => w.MobileUrl).HasColumnName("mobile_url");
        builder.Property(w => w.MobileSha256).HasColumnName("mobile_sha256").HasMaxLength(64);
        builder.Property(w => w.ThumbnailUrl).HasColumnName("thumbnail_url");
        builder.Property(w => w.Tags).HasColumnName("tags");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");

        builder.HasData(SampleWallpapers());
    }

    // Dados fictícios só para destravar o desenvolvimento: URLs example.com e
    // checksums de mentira. O catálogo real substitui isto (Releases do GitHub).
    private static Wallpaper[] SampleWallpapers() =>
    [
        new()
        {
            Id = "aurora-dunes",
            Title = "Aurora Dunes",
            Kind = WallpaperKind.Static,
            DesktopUrl = "https://example.com/wallpapers/aurora-dunes-desktop.png",
            DesktopSha256 = new string('a', 64),
            MobileUrl = "https://example.com/wallpapers/aurora-dunes-mobile.png",
            MobileSha256 = new string('b', 64),
            ThumbnailUrl = "https://example.com/wallpapers/aurora-dunes-thumb.jpg",
            Tags = ["landscape", "dark"],
            CreatedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
        },
        new()
        {
            Id = "neon-tide",
            Title = "Neon Tide",
            Kind = WallpaperKind.Static,
            DesktopUrl = "https://example.com/wallpapers/neon-tide-desktop.png",
            DesktopSha256 = new string('c', 64),
            MobileUrl = "https://example.com/wallpapers/neon-tide-mobile.png",
            MobileSha256 = new string('d', 64),
            ThumbnailUrl = "https://example.com/wallpapers/neon-tide-thumb.jpg",
            Tags = ["abstract", "vibrant"],
            CreatedAt = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero),
        },
        new()
        {
            Id = "drift-loop",
            Title = "Drift Loop",
            Kind = WallpaperKind.Animated,
            DesktopUrl = "https://example.com/wallpapers/drift-loop-desktop.mp4",
            DesktopSha256 = new string('e', 64),
            MobileUrl = null,
            MobileSha256 = null,
            ThumbnailUrl = "https://example.com/wallpapers/drift-loop-thumb.jpg",
            Tags = ["animated", "calm"],
            CreatedAt = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
        },
    ];
}
