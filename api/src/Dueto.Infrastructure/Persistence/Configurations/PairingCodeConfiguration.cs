using Dueto.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dueto.Infrastructure.Persistence.Configurations;

internal sealed class PairingCodeConfiguration : IEntityTypeConfiguration<PairingCode>
{
    public void Configure(EntityTypeBuilder<PairingCode> builder)
    {
        builder.ToTable("pairing_codes");

        builder.HasKey(c => c.Code);
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(16);
        builder.Property(c => c.PairId).HasColumnName("pair_id");
        builder.Property(c => c.ExpiresAt).HasColumnName("expires_at");
        builder.Property(c => c.UsedAt).HasColumnName("used_at");

        // Busca por códigos ainda válidos acontece a toda tentativa de pareamento.
        builder.HasIndex(c => c.ExpiresAt).HasDatabaseName("ix_pairing_codes_expires_at");
    }
}
