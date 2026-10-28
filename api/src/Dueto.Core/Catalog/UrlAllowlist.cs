namespace Dueto.Core.Catalog;

/// <summary>
/// Allowlist de hosts para URLs que o sistema vai acessar ou entregar a clientes. Só https, sem
/// credenciais embutidas, e o host precisa ser um dos permitidos ou um subdomínio deles.
/// </summary>
public static class UrlAllowlist
{
    public const int DefaultMaxLength = 2_048;

    public static bool IsAllowedHttps(string? url, IEnumerable<string> allowedHosts, int maxLength = DefaultMaxLength)
    {
        if (url is null
            || url.Length > maxLength
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var host = uri.IdnHost;

        // O ponto antes do sufixo impede que "notgithub.com" passe como se fosse "github.com".
        return allowedHosts.Any(allowed =>
            host.Equals(allowed, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Uma entrada de allowlist é só o nome do host: sem caminho, porta ou esquema.</summary>
    public static bool IsBareHost(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains('/') && !value.Contains(':');
}
