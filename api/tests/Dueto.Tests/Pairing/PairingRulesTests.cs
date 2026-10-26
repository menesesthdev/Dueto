using Dueto.Core.Pairing;

namespace Dueto.Tests.Pairing;

public class PairingRulesTests
{
    [Fact]
    public void Generated_tokens_are_unique_and_have_256_bits()
    {
        var first = DeviceTokens.Generate();
        var second = DeviceTokens.Generate();

        Assert.NotEqual(first.Value, second.Value);
        Assert.Equal(43, first.Value.Length); // 32 bytes em base64url, sem padding
    }

    [Fact]
    public void Hash_is_deterministic_hex_and_never_equals_the_token()
    {
        var token = DeviceTokens.Generate();

        Assert.Equal(token.Hash, DeviceTokens.Hash(token.Value));
        Assert.Equal(64, token.Hash.Length);
        Assert.NotEqual(token.Value, token.Hash);
        Assert.Matches("^[0-9a-f]{64}$", token.Hash);
    }

    [Fact]
    public void Generated_codes_have_six_digits()
    {
        for (var i = 0; i < 200; i++)
        {
            Assert.True(PairingCodes.IsValidFormat(PairingCodes.Generate()));
        }
    }

    [Theory]
    [InlineData("123456", true)]
    [InlineData("000000", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("12345a", false)]
    [InlineData("123456\n", false)]
    [InlineData(" 123456", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Code_format_is_validated(string? code, bool expected) =>
        Assert.Equal(expected, PairingCodes.IsValidFormat(code));

    [Theory]
    [InlineData("desktop", true)]
    [InlineData("Mobile", true)]
    [InlineData("tablet", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Device_kind_is_parsed(string? value, bool expected) =>
        Assert.Equal(expected, DeviceKinds.TryParse(value, out _));
}
