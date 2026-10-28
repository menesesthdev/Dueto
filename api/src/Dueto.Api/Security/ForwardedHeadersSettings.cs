using System.Net;

namespace Dueto.Api.Security;

/// <summary>
/// Redes das quais a API aceita o cabeçalho <c>X-Forwarded-For</c> (o proxy reverso, o Caddy).
/// Variável de ambiente: <c>ForwardedHeaders__KnownNetworks__0=172.30.0.0/24</c> (notação CIDR).
/// Vazio = o cabeçalho é ignorado e o IP do cliente é o da conexão.
/// </summary>
/// <remarks>
/// Confiar no cabeçalho vindo de qualquer origem deixaria um atacante forjar o próprio IP e escapar do
/// rate limit, por isso só as redes listadas são confiáveis.
/// </remarks>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public string[] KnownNetworks { get; set; } = [];

    public static bool IsValidNetwork(string value) => IPNetwork.TryParse(value, out _);
}
