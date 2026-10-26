using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Dueto.Core.Pairing;

/// <summary>Um token recém-criado: o valor (entregue uma única vez) e o hash (o que vai ao banco).</summary>
public sealed record DeviceToken(string Value, string Hash);

/// <summary>
/// Tokens de dispositivo: 256 bits aleatórios. O banco guarda só o hash SHA-256;
/// como o token já tem entropia alta, não é preciso salt nem hash lento.
/// </summary>
public static class DeviceTokens
{
    /// <summary>Tamanho máximo aceito de um token recebido (um token válido tem 43 caracteres).</summary>
    public const int MaxLength = 128;

    public static DeviceToken Generate()
    {
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return new DeviceToken(value, Hash(value));
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
