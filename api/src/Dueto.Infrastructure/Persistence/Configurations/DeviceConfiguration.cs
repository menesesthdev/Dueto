using Dueto.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dueto.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.PairId).HasColumnName("pair_id");
        builder.Property(d => d.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(d => d.TokenHash).HasColumnName("token_hash").HasMaxLength(128);
        builder.Property(d => d.LastSeenAt).HasColumnName("last_seen_at");

        builder.HasIndex(d => d.PairId).HasDatabaseName("ix_devices_pair_id");
        // O token chega como hash; a busca de autenticação é por ele.
        builder.HasIndex(d => d.TokenHash).IsUnique().HasDatabaseName("ux_devices_token_hash");
    }
}
