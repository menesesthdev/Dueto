namespace Dueto.Core.Catalog;

/// <summary>
/// Importação do catálogo a partir de um arquivo JSON. Variáveis de ambiente: <c>Catalog__ImportFile</c>
/// e <c>Catalog__AllowedHosts__0</c>.
/// </summary>
public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>Hosts aceitos por padrão: os arquivos dos wallpapers ficam nos Releases do GitHub.</summary>
    public static readonly string[] DefaultAllowedHosts = ["github.com"];

    /// <summary>Caminho do catalog.json. Vazio = nenhuma importação.</summary>
    public string? ImportFile { get; set; }

    /// <summary>
    /// Hosts permitidos nas URLs do catálogo. Vazio usa <see cref="DefaultAllowedHosts"/> (o binder do .NET
    /// somaria um valor padrão de array aos valores configurados).
    /// </summary>
    public string[] AllowedHosts { get; set; } = [];

    public IReadOnlyList<string> EffectiveAllowedHosts =>
        AllowedHosts.Length > 0 ? AllowedHosts : DefaultAllowedHosts;

    public string? Validate() =>
        AllowedHosts.All(UrlAllowlist.IsBareHost) ? null : "Catalog:AllowedHosts must contain bare host names.";
}
