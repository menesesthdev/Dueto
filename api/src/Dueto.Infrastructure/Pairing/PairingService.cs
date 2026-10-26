using Dueto.Core.Domain;
using Dueto.Core.Pairing;
using Dueto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dueto.Infrastructure.Pairing;

internal sealed class PairingService(
    DuetoDbContext db,
    TimeProvider timeProvider,
    IOptions<PairingOptions> options) : IPairingService
{
    private const int MaxCodeGenerationAttempts = 10;

    public async Task<PairingStarted> StartAsync(
        DeviceKind kind,
        string deviceName,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.Value.CodeTtlMinutes);
        var token = DeviceTokens.Generate();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Códigos expirados ou usados não servem mais; limpar libera o espaço de 1 milhão de códigos.
        await db.PairingCodes
            .Where(c => c.ExpiresAt <= now || c.UsedAt != null)
            .ExecuteDeleteAsync(cancellationToken);

        var code = await GenerateUnusedCodeAsync(cancellationToken);

        var pair = new Pair { Id = Guid.NewGuid(), CreatedAt = now };
        db.Pairs.Add(pair);
        db.Devices.Add(new Device
        {
            Id = Guid.NewGuid(),
            PairId = pair.Id,
            Kind = kind,
            Name = deviceName,
            TokenHash = token.Hash,
        });
        db.PairStates.Add(new PairState { PairId = pair.Id, Version = 0, UpdatedAt = now });
        db.PairingCodes.Add(new PairingCode { Code = code, PairId = pair.Id, ExpiresAt = expiresAt });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PairingStarted(token.Value, code, expiresAt);
    }

    public async Task<PairingCompleteResult> CompleteAsync(
        string code,
        DeviceKind kind,
        string deviceName,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var pairingCode = await db.PairingCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Code == code, cancellationToken);

        if (pairingCode is null || pairingCode.UsedAt is not null || pairingCode.ExpiresAt <= now)
        {
            return new PairingCompleteResult(PairingCompleteStatus.InvalidCode);
        }

        var kindTaken = await db.Devices
            .AsNoTracking()
            .AnyAsync(d => d.PairId == pairingCode.PairId && d.Kind == kind, cancellationToken);

        if (kindTaken)
        {
            return new PairingCompleteResult(PairingCompleteStatus.KindConflict);
        }

        var token = DeviceTokens.Generate();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Uso único atômico: só uma requisição concorrente consegue marcar o código como usado.
        var claimed = await db.PairingCodes
            .Where(c => c.Code == code && c.UsedAt == null && c.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, now), cancellationToken);

        if (claimed == 0)
        {
            return new PairingCompleteResult(PairingCompleteStatus.InvalidCode);
        }

        db.Devices.Add(new Device
        {
            Id = Guid.NewGuid(),
            PairId = pairingCode.PairId,
            Kind = kind,
            Name = deviceName,
            TokenHash = token.Hash,
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PairingCompleteResult(PairingCompleteStatus.Success, token.Value);
    }

    private async Task<string> GenerateUnusedCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
        {
            var candidate = PairingCodes.Generate();
            if (!await db.PairingCodes.AnyAsync(c => c.Code == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate an unused pairing code.");
    }
}
