using Dueto.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dueto.Infrastructure.Persistence.Configurations;

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("reminders");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.DeviceId).HasColumnName("device_id");
        builder.Property(r => r.Version).HasColumnName("version");
        builder.Property(r => r.Sequence).HasColumnName("sequence");
        builder.Property(r => r.DueAt).HasColumnName("due_at");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);

        // O agendador varre lembretes pendentes já vencidos em intervalo curto.
        builder.HasIndex(r => new { r.Status, r.DueAt }).HasDatabaseName("ix_reminders_status_due_at");
        builder.HasIndex(r => r.DeviceId).HasDatabaseName("ix_reminders_device_id");
    }
}
