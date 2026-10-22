using System.Text.RegularExpressions;

namespace Dueto.Core.Catalog;

/// <summary>
/// Regra do identificador de wallpaper (slug). É a mesma regra do protocolo
/// <c>dueto://apply?id=</c>: só minúsculas, dígitos e hífen, de 1 a 64 caracteres.
/// </summary>
public static partial class WallpaperId
{
    public const int MaxLength = 64;

    // \z (e não $): em .NET o $ também casa antes de um "\n" final, o que deixaria passar "id\n".
    [GeneratedRegex(@"^[a-z0-9-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public static bool IsValid(string? value) =>
        value is not null && SlugPattern().IsMatch(value);
}
