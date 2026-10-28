using System.Buffers.Text;
using System.Security.Cryptography;
using Dueto.Infrastructure.Push;

namespace Dueto.Tests.Push;

/// <summary>
/// Valida a criptografia contra os vetores oficiais do RFC 8291 (Apêndice A). Se o resultado bater byte
/// a byte com o RFC, a implementação está correta, independentemente de qualquer push service.
/// </summary>
public class Aes128GcmEncryptionTests
{
    // RFC 8291, Apêndice A e seção 5.
    private const string Plaintext = "V2hlbiBJIGdyb3cgdXAsIEkgd2FudCB0byBiZSBhIHdhdGVybWVsb24";
    private const string AsPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";
    private const string AsPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string UaPublic = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string AuthSecret = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string Salt = "DGv6ra1nlYgDCS1FRnbzlw";
    private const string ExpectedBody =
        "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN";

    private static byte[] B64(string value) => Base64Url.DecodeFromChars(value);

    private static ECDiffieHellman ServerKeyFromRfc()
    {
        var publicKey = B64(AsPublic);
        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = B64(AsPrivate),
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] },
        });
    }

    [Fact]
    public void Reproduces_the_rfc_8291_example_byte_for_byte()
    {
        using var serverKey = ServerKeyFromRfc();

        var body = Aes128GcmEncryption.Encrypt(B64(Plaintext), B64(UaPublic), B64(AuthSecret), serverKey, B64(Salt));

        Assert.Equal(ExpectedBody, Base64Url.EncodeToString(body));
    }

    [Fact]
    public void Body_has_the_rfc_8188_header_layout()
    {
        using var serverKey = ServerKeyFromRfc();

        var body = Aes128GcmEncryption.Encrypt(B64(Plaintext), B64(UaPublic), B64(AuthSecret), serverKey, B64(Salt));

        Assert.Equal(86 + 41 + 1 + 16, body.Length); // cabeçalho + texto + delimitador + tag
        Assert.Equal(B64(Salt), body[..16]);
        Assert.Equal([0x00, 0x00, 0x10, 0x00], body[16..20]); // registro de 4096 bytes
        Assert.Equal(65, body[20]);
        Assert.Equal(B64(AsPublic), body[21..86]);
    }

    [Fact]
    public void Each_encryption_uses_a_fresh_key_and_salt()
    {
        var first = Aes128GcmEncryption.Encrypt([1, 2, 3], B64(UaPublic), B64(AuthSecret));
        var second = Aes128GcmEncryption.Encrypt([1, 2, 3], B64(UaPublic), B64(AuthSecret));

        Assert.NotEqual(first, second);
        Assert.NotEqual(first[..16], second[..16]);
        Assert.NotEqual(first[21..86], second[21..86]);
    }

    [Fact]
    public void Payload_at_the_limit_is_accepted_and_one_byte_more_is_rejected()
    {
        var atLimit = new byte[Aes128GcmEncryption.MaxPayloadLength];

        var body = Aes128GcmEncryption.Encrypt(atLimit, B64(UaPublic), B64(AuthSecret));

        Assert.Equal(Aes128GcmEncryption.RecordSize, body.Length);
        Assert.Throws<ArgumentException>(() =>
            Aes128GcmEncryption.Encrypt(new byte[atLimit.Length + 1], B64(UaPublic), B64(AuthSecret)));
    }

    [Fact]
    public void A_point_that_is_not_on_the_curve_is_rejected()
    {
        var invalid = B64(UaPublic);
        invalid[64] ^= 0xFF;

        Assert.ThrowsAny<Exception>(() => Aes128GcmEncryption.Encrypt([1], invalid, B64(AuthSecret)));
    }

    [Theory]
    [InlineData(64)]
    [InlineData(66)]
    public void A_public_key_with_the_wrong_length_is_rejected(int length) =>
        Assert.Throws<ArgumentException>(() => Aes128GcmEncryption.Encrypt([1], new byte[length], B64(AuthSecret)));

    [Fact]
    public void An_auth_secret_with_the_wrong_length_is_rejected() =>
        Assert.Throws<ArgumentException>(() => Aes128GcmEncryption.Encrypt([1], B64(UaPublic), new byte[15]));
}
