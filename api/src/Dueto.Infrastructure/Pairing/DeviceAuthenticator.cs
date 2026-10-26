using Dueto.Core.Pairing;
using Dueto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dueto.Infrastructure.Pairing;

internal sealed class DeviceAuthenticator(DuetoDbContext db) : IDeviceAuthenticator
{
    public async Task<DeviceIdentity?> AuthenticateAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token) || token.Length > DeviceTokens.MaxLength)
        {
            return null;
        }

        var hash = DeviceTokens.Hash(token);

        return await db.Devices
            .AsNoTracking()
            .Where(d => d.TokenHash == hash)
            .Select(d => new DeviceIdentity(d.Id, d.PairId, d.Kind))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
