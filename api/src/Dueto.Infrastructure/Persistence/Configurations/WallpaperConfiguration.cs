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
    }
}
