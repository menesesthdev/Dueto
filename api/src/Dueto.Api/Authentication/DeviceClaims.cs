using System.Security.Claims;
using Dueto.Core.Domain;
using Dueto.Core.Pairing;

namespace Dueto.Api.Authentication;

/// <summary>Ida e volta entre <see cref="DeviceIdentity"/> e o <see cref="ClaimsPrincipal"/> da requisição.</summary>
public static class DeviceClaims
{
    private const string DeviceIdClaim = "device_id";
    private const string PairIdClaim = "pair_id";
    private const string KindClaim = "device_kind";

    public static ClaimsPrincipal ToPrincipal(DeviceIdentity identity, string scheme)
    {
        var claims = new[]
        {
            new Claim(DeviceIdClaim, identity.DeviceId.ToString()),
            new Claim(PairIdClaim, identity.PairId.ToString()),
            new Claim(KindClaim, DeviceKinds.ToWire(identity.Kind)),
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }

    public static DeviceIdentity ToDeviceIdentity(this ClaimsPrincipal principal)
    {
        var deviceId = Guid.Parse(principal.FindFirstValue(DeviceIdClaim)!);
        var pairId = Guid.Parse(principal.FindFirstValue(PairIdClaim)!);
        DeviceKinds.TryParse(principal.FindFirstValue(KindClaim), out var kind);
        return new DeviceIdentity(deviceId, pairId, kind);
    }
}
