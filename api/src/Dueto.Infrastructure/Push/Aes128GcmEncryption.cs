using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Dueto.Infrastructure.Push;

/// <summary>
/// Criptografia do payload do Web Push: RFC 8291 sobre o content coding <c>aes128gcm</c> do RFC 8188.
/// Um único registro, sem padding extra. Só usa as primitivas do próprio .NET (ECDH, HKDF, AES-GCM).
/// </summary>
internal static class Aes128GcmEncryption
{
    public const int RecordSize = 4_096;
    public const int PublicKeyLength = 65;
    public const int AuthSecretLength = 16;
    private const int SaltLength = 16;
    private const int TagLength = 16;
    private const int HeaderLength = SaltLength + 4 + 1 + PublicKeyLength;

    /// <summary>Maior payload que cabe em um único registro de 4096 bytes.</summary>
    public const int MaxPayloadLength = RecordSize - HeaderLength - TagLength - 1;

    /// <param name="payload">Texto a cifrar.</param>
    /// <param name="userAgentPublicKey">Chave p256dh da assinatura: ponto P-256 não comprimido (65 bytes).</param>
    /// <param name="authSecret">Segredo "auth" da assinatura (16 bytes).</param>
    /// <returns>O corpo da requisição ao push service: cabeçalho de 86 bytes + registro cifrado.</returns>
    public static byte[] Encrypt(byte[] payload, byte[] userAgentPublicKey, byte[] authSecret)
    {
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return Encrypt(payload, userAgentPublicKey, authSecret, ephemeral, RandomNumberGenerator.GetBytes(SaltLength));
    }

    /// <summary>Versão com chave efêmera e salt injetados, para reproduzir os vetores do RFC 8291 nos testes.</summary>
    internal static byte[] Encrypt(
        byte[] payload,
        byte[] userAgentPublicKey,
        byte[] authSecret,
        ECDiffieHellman applicationServerKey,
        byte[] salt)
    {
        if (payload.Length > MaxPayloadLength)
        {
            throw new ArgumentException("Payload does not fit in a single record.", nameof(payload));
        }

        if (authSecret.Length != AuthSecretLength || salt.Length != SaltLength)
        {
            throw new ArgumentException("Invalid auth secret or salt length.");
        }

        var applicationServerPublicKey = ExportUncompressedPoint(applicationServerKey);
        using var userAgentKey = ImportPublicKey(userAgentPublicKey);

        // ECDH: segredo compartilhado (coordenada X) entre a chave efêmera do servidor e a do navegador.
        var ecdhSecret = applicationServerKey.DeriveRawSecretAgreement(userAgentKey.PublicKey);

        // IKM = HKDF(salt: auth_secret, ikm: ecdh_secret, info: "WebPush: info" || 0x00 || ua_public || as_public)
        var keyInfo = Concat(Encoding.ASCII.GetBytes("WebPush: info\0"), userAgentPublicKey, applicationServerPublicKey);
        var prkKey = HKDF.Extract(HashAlgorithmName.SHA256, ecdhSecret, authSecret);
        var ikm = HKDF.Expand(HashAlgorithmName.SHA256, prkKey, 32, keyInfo);

        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var contentEncryptionKey = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        // O delimitador 0x02 marca o último (e único) registro.
        var plaintext = Concat(payload, [0x02]);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        using (var aes = new AesGcm(contentEncryptionKey, TagLength))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // Cabeçalho (RFC 8188): salt(16) || tamanho do registro(4, big-endian) || idlen(1) || keyid(65).
        var header = new byte[HeaderLength];
        salt.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(SaltLength, 4), RecordSize);
        header[SaltLength + 4] = PublicKeyLength;
        applicationServerPublicKey.CopyTo(header, SaltLength + 5);

        return Concat(header, ciphertext, tag);
    }

    internal static byte[] ExportUncompressedPoint(ECDiffieHellman key)
    {
        var point = key.ExportParameters(false).Q;
        return Concat([0x04], point.X!, point.Y!);
    }

    /// <summary>Importa o ponto P-256 não comprimido. Falha se o ponto não estiver na curva.</summary>
    internal static ECDiffieHellman ImportPublicKey(byte[] uncompressedPoint)
    {
        if (uncompressedPoint.Length != PublicKeyLength || uncompressedPoint[0] != 0x04)
        {
            throw new ArgumentException("The public key must be an uncompressed P-256 point (65 bytes).");
        }

        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = uncompressedPoint[1..33], Y = uncompressedPoint[33..65] },
        });
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }
}
