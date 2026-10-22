using Dueto.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dueto.Infrastructure.Persistence.Configurations;

internal sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("push_subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.DeviceId).HasColumnName("device_id");
        builder.Property(s => s.Endpoint).HasColumnName("endpoint");
        builder.Property(s => s.P256dh).HasColumnName("p256dh");
        builder.Property(s => s.Auth).HasColumnName("auth");

        builder.HasIndex(s => s.DeviceId).HasDatabaseName("ix_push_subscriptions_device_id");
        builder.HasIndex(s => s.Endpoint).IsUnique().HasDatabaseName("ux_push_subscriptions_endpoint");
    }
}
