using System.Buffers.Text;
using System.Security.Cryptography;

namespace Dueto.Tests.Infrastructure;

/// <summary>Par de chaves VAPID válido (mesmo formato do <c>npx web-push generate-vapid-keys</c>).</summary>
public sealed record VapidTestKeys(string PublicKey, string PrivateKey)
{
    public static VapidTestKeys Generate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(true);
        return new VapidTestKeys(
            Base64Url.EncodeToString([0x04, .. parameters.Q.X!, .. parameters.Q.Y!]),
            Base64Url.EncodeToString(parameters.D!));
    }
}
