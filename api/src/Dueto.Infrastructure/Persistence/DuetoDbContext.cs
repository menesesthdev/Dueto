using Dueto.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dueto.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core do Dueto. As configurações de cada entidade ficam em
/// classes <see cref="IEntityTypeConfiguration{TEntity}"/> separadas, aplicadas
/// automaticamente a partir deste assembly.
/// </summary>
public class DuetoDbContext(DbContextOptions<DuetoDbContext> options) : DbContext(options)
{
    public DbSet<Wallpaper> Wallpapers => Set<Wallpaper>();
    public DbSet<Pair> Pairs => Set<Pair>();
    public DbSet<PairingCode> PairingCodes => Set<PairingCode>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<PairState> PairStates => Set<PairState>();
    public DbSet<DeviceApplied> DeviceApplied => Set<DeviceApplied>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DuetoDbContext).Assembly);
    }
}
