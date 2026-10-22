using Dueto.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dueto.Infrastructure.Persistence.Configurations;

internal sealed class PairStateConfiguration : IEntityTypeConfiguration<PairState>
{
    public void Configure(EntityTypeBuilder<PairState> builder)
    {
        builder.ToTable("pair_state");

        // Uma linha de estado por par.
        builder.HasKey(s => s.PairId);
        builder.Property(s => s.PairId).HasColumnName("pair_id").ValueGeneratedNever();
        builder.Property(s => s.WallpaperId).HasColumnName("wallpaper_id").HasMaxLength(64);
        builder.Property(s => s.Version).HasColumnName("version");
        builder.Property(s => s.OriginDeviceId).HasColumnName("origin_device_id");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
    }
}
