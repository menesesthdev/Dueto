using Dueto.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dueto.Infrastructure.Persistence.Configurations;

internal sealed class PairConfiguration : IEntityTypeConfiguration<Pair>
{
    public void Configure(EntityTypeBuilder<Pair> builder)
    {
        builder.ToTable("pairs");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");

        builder.HasMany(p => p.Devices)
            .WithOne()
            .HasForeignKey(d => d.PairId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
