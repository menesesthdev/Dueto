using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dueto.Infrastructure.Push;
using Dueto.Tests.Infrastructure;

namespace Dueto.Tests.Push;

public class VapidSignerTests
{
    private const string Subject = "mailto:owner@example.com";
    private const string Audience = "https://web.push.apple.com";

    private readonly VapidTestKeys _keys = VapidTestKeys.Generate();
    private readonly TestClock _clock = new();

    private VapidSigner CreateSigner() => new(_keys.PublicKey, _keys.PrivateKey, Subject, _clock);

    private static string TokenOf(string authorizationHeader) =>
        authorizationHeader["vapid t=".Length..authorizationHeader.IndexOf(", k=", StringComparison.Ordinal)];

    /// <summary>Confere estrutura, claims e assinatura ES256 de um JWT VAPID.</summary>
    internal static void AssertValidJwt(string jwt, string publicKey, string expectedAudience, string expectedSubject, DateTimeOffset now)
    {
        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);

        using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
        Assert.Equal("JWT", header.RootElement.GetProperty("typ").GetString());
        Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());

        using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        Assert.Equal(expectedAudience, claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal(expectedSubject, claims.RootElement.GetProperty("sub").GetString());

        var expiresIn = DateTimeOffset.FromUnixTimeSeconds(claims.RootElement.GetProperty("exp").GetInt64()) - now;
        Assert.InRange(expiresIn, TimeSpan.FromHours(1), TimeSpan.FromHours(24)); // a Apple recusa mais de 1 dia

        var publicBytes = Base64Url.DecodeFromChars(publicKey);
        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = publicBytes[1..33], Y = publicBytes[33..65] },
        });
        var signature = Base64Url.DecodeFromChars(parts[2]);

        Assert.Equal(64, signature.Length); // r||s cru, não DER
        Assert.True(verifier.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            signature,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    public void Authorization_header_follows_rfc_8292()
    {
        using var signer = CreateSigner();

        var header = signer.AuthorizationHeader(Audience);

        Assert.StartsWith("vapid t=", header);
        Assert.EndsWith($", k={_keys.PublicKey}", header);
        AssertValidJwt(TokenOf(header), _keys.PublicKey, Audience, Subject, _clock.GetUtcNow());
    }

    [Fact]
    public void The_token_is_reused_for_the_same_audience_within_an_hour()
    {
        using var signer = CreateSigner();

        var first = signer.AuthorizationHeader(Audience);
        _clock.Advance(TimeSpan.FromMinutes(59));

        Assert.Equal(first, signer.AuthorizationHeader(Audience));
    }

    [Fact]
    public void The_token_is_renewed_after_an_hour()
    {
        using var signer = CreateSigner();

        var first = TokenOf(signer.AuthorizationHeader(Audience));
        _clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        var second = TokenOf(signer.AuthorizationHeader(Audience));

        Assert.NotEqual(first, second);
        AssertValidJwt(second, _keys.PublicKey, Audience, Subject, _clock.GetUtcNow());
    }

    [Fact]
    public void Each_audience_gets_its_own_token()
    {
        using var signer = CreateSigner();

        var apple = signer.AuthorizationHeader(Audience);
        var google = signer.AuthorizationHeader("https://fcm.googleapis.com");

        Assert.NotEqual(TokenOf(apple), TokenOf(google));
        AssertValidJwt(TokenOf(google), _keys.PublicKey, "https://fcm.googleapis.com", Subject, _clock.GetUtcNow());
    }

    [Fact]
    public void A_token_signed_by_another_key_does_not_verify()
    {
        using var signer = CreateSigner();
        var token = TokenOf(signer.AuthorizationHeader(Audience));
        var other = VapidTestKeys.Generate();

        Assert.ThrowsAny<Exception>(() => AssertValidJwt(token, other.PublicKey, Audience, Subject, _clock.GetUtcNow()));
    }
}
