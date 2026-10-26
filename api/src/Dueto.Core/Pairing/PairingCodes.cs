using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Dueto.Core.Pairing;

/// <summary>Código de pareamento: 6 dígitos, gerado com fonte criptograficamente segura.</summary>
public static partial class PairingCodes
{
    public const int Length = 6;

    [GeneratedRegex(@"^[0-9]{6}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static string Generate() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public static bool IsValidFormat(string? value) => value is not null && Pattern().IsMatch(value);
}
