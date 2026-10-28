using System.Net;
using Dueto.Api.Security;
using Dueto.Tests.Infrastructure;

namespace Dueto.Tests.Api;

/// <summary>
/// IP real do cliente atrás do Caddy. O rate limit é por IP, então se a API enxergasse sempre o IP do proxy,
/// todos os usuários dividiriam um único limite; e se confiasse em qualquer cabeçalho, um atacante forjaria o IP.
/// Usa uma rota que responde 400 antes de tocar no banco.
/// </summary>
public class ForwardedHeadersTests
{
    private const string Route = "/api/wallpapers/Bad_Id";
    private const string TrustedProxy = "10.1.1.1";

    private static Dictionary<string, string?> Settings(bool trustProxyNetwork)
    {
        var settings = new Dictionary<string, string?>
        {
            ["RateLimiting:PublicPermitLimit"] = "1",
            ["RateLimiting:PublicWindowSeconds"] = "3600",
        };

        if (trustProxyNetwork)
        {
            settings["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/8";
        }

        return settings;
    }

    private static async Task<HttpStatusCode> Get(HttpClient client, string remoteIp, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add(RemoteIpApiFactory.RemoteIpHeader, remoteIp);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return (await client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task Clients_behind_a_trusted_proxy_get_separate_rate_limits()
    {
        using var factory = new RemoteIpApiFactory(Settings(trustProxyNetwork: true));
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, await Get(client, TrustedProxy, "198.51.100.1"));
        Assert.Equal(HttpStatusCode.BadRequest, await Get(client, TrustedProxy, "198.51.100.2")); // outro cliente real
        Assert.Equal(HttpStatusCode.TooManyRequests, await Get(client, TrustedProxy, "198.51.100.1")); // o primeiro de novo
    }

    [Fact]
    public async Task Without_forwarded_headers_all_clients_behind_the_proxy_share_one_limit()
    {
        // É o problema que a configuração resolve: sem ela, o IP visto é sempre o do proxy.
        using var factory = new RemoteIpApiFactory(Settings(trustProxyNetwork: false));
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, await Get(client, TrustedProxy, "198.51.100.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Get(client, TrustedProxy, "198.51.100.2"));
    }

    [Fact]
    public async Task A_forged_header_from_an_untrusted_source_does_not_escape_the_limit()
    {
        using var factory = new RemoteIpApiFactory(Settings(trustProxyNetwork: true));
        var client = factory.CreateClient();
        const string attacker = "203.0.113.9"; // fora de 10.0.0.0/8

        Assert.Equal(HttpStatusCode.BadRequest, await Get(client, attacker, "198.51.100.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Get(client, attacker, "198.51.100.2"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Get(client, attacker, "198.51.100.3"));
    }

    [Fact]
    public async Task The_limit_is_per_real_client_even_when_the_proxy_is_not_the_first_request()
    {
        using var factory = new RemoteIpApiFactory(Settings(trustProxyNetwork: true));
        var client = factory.CreateClient();

        await Get(client, TrustedProxy, "198.51.100.1");

        // Um terceiro cliente, por outro proxy da mesma rede confiável, ainda tem a sua própria cota.
        Assert.Equal(HttpStatusCode.BadRequest, await Get(client, "10.1.1.2", "198.51.100.7"));
    }

    [Theory]
    [InlineData("not-a-network")]
    [InlineData("10.0.0.0/33")]
    [InlineData("*")]
    public void App_refuses_to_start_with_an_invalid_network(string network)
    {
        using var factory = new RemoteIpApiFactory(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownNetworks:0"] = network,
        });

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Theory]
    [InlineData("172.30.0.0/24", true)]
    [InlineData("10.0.0.0/8", true)]
    [InlineData("::1/128", true)]
    [InlineData("172.30.0.0", false)]
    [InlineData("172.30.0.0/99", false)]
    [InlineData("", false)]
    public void Network_notation_is_validated(string value, bool expected) =>
        Assert.Equal(expected, ForwardedHeadersSettings.IsValidNetwork(value));
}
