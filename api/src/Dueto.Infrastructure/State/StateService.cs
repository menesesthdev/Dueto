using Dueto.Core.Domain;
using Dueto.Core.Pairing;
using Dueto.Core.State;
using Dueto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dueto.Infrastructure.State;

internal sealed class StateService(
    DuetoDbContext db,
    TimeProvider timeProvider,
    IStateNotifier notifier,
    ILogger<StateService> logger) : IStateService
{
    public async Task<StateSnapshot> GetAsync(DeviceIdentity identity, CancellationToken cancellationToken)
    {
        var state = await db.PairStates
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.PairId == identity.PairId, cancellationToken);

        var devices = await db.Devices
            .AsNoTracking()
            .Where(d => d.PairId == identity.PairId)
            .OrderBy(d => d.Kind)
            .Select(d => new { d.Id, d.Kind })
            .ToListAsync(cancellationToken);

        var deviceIds = devices.Select(d => d.Id).ToList();
        var applied = await db.DeviceApplied
            .AsNoTracking()
            .Where(a => deviceIds.Contains(a.DeviceId))
            .ToDictionaryAsync(a => a.DeviceId, cancellationToken);

        var appliedByDevice = devices
            .Select(d =>
            {
                applied.TryGetValue(d.Id, out var row);
                return new DeviceAppliedDto(d.Id, DeviceKinds.ToWire(d.Kind), row?.WallpaperId, row?.Version, row?.AppliedAt);
            })
            .ToList();

        return new StateSnapshot(state?.WallpaperId, state?.Version ?? 0, state?.OriginDeviceId, appliedByDevice);
    }

    public async Task<SetStateResult> SetAsync(
        DeviceIdentity identity,
        string wallpaperId,
        CancellationToken cancellationToken)
    {
        if (!await db.Wallpapers.AnyAsync(w => w.Id == wallpaperId, cancellationToken))
        {
            return new SetStateResult(SetStateStatus.WallpaperNotFound);
        }

        var now = timeProvider.GetUtcNow();
        long version;

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            // O UPDATE tranca a linha do par até o commit: duas trocas simultâneas são
            // serializadas e cada uma recebe uma versão distinta e crescente.
            var updated = await db.PairStates
                .Where(s => s.PairId == identity.PairId)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(p => p.Version, p => p.Version + 1)
                        .SetProperty(p => p.WallpaperId, wallpaperId)
                        .SetProperty(p => p.OriginDeviceId, identity.DeviceId)
                        .SetProperty(p => p.UpdatedAt, now),
                    cancellationToken);

            if (updated == 0)
            {
                throw new InvalidOperationException("Pair state row is missing for an existing pair.");
            }

            version = await db.PairStates
                .AsNoTracking()
                .Where(s => s.PairId == identity.PairId)
                .Select(s => s.Version)
                .SingleAsync(cancellationToken);

            // Troca originada no desktop: o servidor já marca o desktop como aplicado.
            // Troca originada no mobile: fica pendente até o Atalho confirmar.
            if (identity.Kind == DeviceKind.Desktop)
            {
                await UpsertAppliedAsync(identity.DeviceId, wallpaperId, version, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await NotifyOthersAsync(identity, wallpaperId, version, cancellationToken);

        return new SetStateResult(SetStateStatus.Success, version);
    }

    public async Task<ApplyStatus> ApplyAsync(
        DeviceIdentity identity,
        string wallpaperId,
        long version,
        CancellationToken cancellationToken)
    {
        var state = await db.PairStates
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.PairId == identity.PairId, cancellationToken);

        var currentVersion = state?.Version ?? 0;

        if (version < 1 || version > currentVersion)
        {
            return ApplyStatus.InvalidVersion;
        }

        if (version == currentVersion && state?.WallpaperId != wallpaperId)
        {
            return ApplyStatus.Mismatch;
        }

        if (!await db.Wallpapers.AnyAsync(w => w.Id == wallpaperId, cancellationToken))
        {
            return ApplyStatus.WallpaperNotFound;
        }

        await UpsertAppliedAsync(identity.DeviceId, wallpaperId, version, timeProvider.GetUtcNow(), cancellationToken);

        return ApplyStatus.Success;
    }

    /// <summary>
    /// Grava a confirmação só se a versão for maior que a já gravada: confirmar duas vezes
    /// a mesma versão (idempotência) ou uma versão antiga nunca faz o dispositivo "voltar".
    /// </summary>
    private async Task UpsertAppliedAsync(
        Guid deviceId,
        string wallpaperId,
        long version,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO device_applied (device_id, wallpaper_id, version, applied_at)
            VALUES ({deviceId}, {wallpaperId}, {version}, {now})
            ON CONFLICT (device_id) DO UPDATE
            SET wallpaper_id = EXCLUDED.wallpaper_id,
                version = EXCLUDED.version,
                applied_at = EXCLUDED.applied_at
            WHERE device_applied.version < EXCLUDED.version
            """,
            cancellationToken);
    }

    private async Task NotifyOthersAsync(
        DeviceIdentity origin,
        string wallpaperId,
        long version,
        CancellationToken cancellationToken)
    {
        // Sem eco: quem originou a troca não recebe o próprio evento.
        var recipients = await db.Devices
            .AsNoTracking()
            .Where(d => d.PairId == origin.PairId && d.Id != origin.DeviceId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            return;
        }

        try
        {
            await notifier.NotifyStateChangedAsync(
                new StateChangedMessage(wallpaperId, version, origin.DeviceId),
                recipients,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // O estado já foi gravado. Falha de entrega em tempo real não pode desfazer a troca:
            // o dispositivo recebe o snapshot ao reconectar.
            logger.LogWarning(exception, "Failed to deliver StateChanged for version {Version}.", version);
        }
    }
}
