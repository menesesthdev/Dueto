namespace Dueto.Api.Security;

/// <summary>
/// Origens do navegador autorizadas a chamar a API (o domínio do PWA).
/// Vem de variável de ambiente: <c>Cors__AllowedOrigins__0</c>, <c>Cors__AllowedOrigins__1</c>...
/// Lista vazia significa que nenhuma origem de navegador é aceita.
/// </summary>
public sealed class CorsPolicyOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "pwa";

    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Origem válida: URL absoluta só com esquema e host (sem caminho, sem curinga).
    /// HTTPS é obrigatório; HTTP só é aceito para localhost, em desenvolvimento.
    /// </summary>
    public static bool IsValidOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var isOriginOnly = uri.AbsolutePath == "/"
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && !origin.EndsWith('/');

        var schemeAllowed = uri.Scheme == Uri.UriSchemeHttps
            || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);

        return isOriginOnly && schemeAllowed;
    }
}
