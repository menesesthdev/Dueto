using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dueto.Infrastructure.Push;

/// <summary>
/// Identificação do servidor de aplicação (VAPID, RFC 8292): um JWT ES256 assinado com a chave
/// privada VAPID. O push service usa a chave pública (e a do <c>PushManager.subscribe</c>) para
/// confirmar que quem envia é quem criou a assinatura.
/// </summary>
internal sealed class VapidSigner : IDisposable
{
    // A Apple recusa expiração a mais de 1 dia e pede que o JWT não seja renovado mais de 1 vez por hora.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan TokenReuse = TimeSpan.FromHours(1);

    private readonly ECDsa _signingKey;
    private readonly string _subject;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, (string Token, DateTimeOffset IssuedAt)> _cache = [];
    private readonly Lock _gate = new();

    public VapidSigner(string publicKey, string privateKey, string subject, TimeProvider timeProvider)
    {
        PublicKey = publicKey;
        _subject = subject;
        _timeProvider = timeProvider;

        var publicBytes = Base64Url.DecodeFromChars(publicKey);
        _signingKey = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64Url.DecodeFromChars(privateKey),
            Q = new ECPoint { X = publicBytes[1..33], Y = publicBytes[33..65] },
        });
    }

    public string PublicKey { get; }

    /// <summary>Valor do cabeçalho <c>Authorization</c> para um push service (RFC 8292, seção 3).</summary>
    public string AuthorizationHeader(string audience) => $"vapid t={TokenFor(audience)}, k={PublicKey}";

    private string TokenFor(string audience)
    {
        var now = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            if (_cache.TryGetValue(audience, out var cached) && now - cached.IssuedAt < TokenReuse)
            {
                return cached.Token;
            }

            var token = CreateToken(audience, now);
            _cache[audience] = (token, now);
            return token;
        }
    }

    internal string CreateToken(string audience, DateTimeOffset now)
    {
        var header = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("""{"typ":"JWT","alg":"ES256"}"""));
        var claims = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new
        {
            aud = audience,
            exp = (now + TokenLifetime).ToUnixTimeSeconds(),
            sub = _subject,
        }));

        var signingInput = $"{header}.{claims}";

        // ES256 no JWT usa a assinatura "crua" r||s (64 bytes), não o formato DER.
        var signature = _signingKey.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    public void Dispose() => _signingKey.Dispose();
}
