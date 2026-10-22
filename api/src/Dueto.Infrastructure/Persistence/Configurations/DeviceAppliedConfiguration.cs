using Dueto.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dueto.Infrastructure.Persistence.Configurations;

internal sealed class DeviceAppliedConfiguration : IEntityTypeConfiguration<DeviceApplied>
{
    public void Configure(EntityTypeBuilder<DeviceApplied> builder)
    {
        builder.ToTable("device_applied");

        // Uma linha por dispositivo: o último wallpaper/versão que ele aplicou.
        builder.HasKey(a => a.DeviceId);
        builder.Property(a => a.DeviceId).HasColumnName("device_id").ValueGeneratedNever();
        builder.Property(a => a.WallpaperId).HasColumnName("wallpaper_id").HasMaxLength(64);
        builder.Property(a => a.Version).HasColumnName("version");
        builder.Property(a => a.AppliedAt).HasColumnName("applied_at");
    }
}
