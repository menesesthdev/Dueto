using System.Net;
using System.Net.Http.Json;
using Dueto.Tests.Infrastructure;

namespace Dueto.Tests.Api;

/// <summary>
/// Rate limit do pareamento. Usa códigos malformados, rejeitados com 400 antes de tocar
/// no banco, então não precisa de Postgres.
/// </summary>
public class PairingRateLimitTests
{
    private static Task<HttpResponseMessage> CompleteWithMalformedCode(HttpClient client) =>
        client.PostAsJsonAsync("/api/pairing/complete", new { code = "bad", deviceName = "x", kind = "mobile" });

    [Fact]
    public async Task Pairing_is_limited_per_ip()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:PairingPermitLimit"] = "2",
            ["RateLimiting:PairingCompleteGlobalPermitLimit"] = "1000",
        });
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteWithMalformedCode(client)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteWithMalformedCode(client)).StatusCode);

        var limited = await CompleteWithMalformedCode(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Complete_has_a_global_cap_across_all_ips()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:PairingPermitLimit"] = "1000",
            ["RateLimiting:PairingCompleteGlobalPermitLimit"] = "2",
        });
        var client = factory.CreateClient();

        await CompleteWithMalformedCode(client);
        await CompleteWithMalformedCode(client);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await CompleteWithMalformedCode(client)).StatusCode);
    }

    [Fact]
    public async Task Global_cap_does_not_affect_start()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:PairingPermitLimit"] = "1000",
            ["RateLimiting:PairingCompleteGlobalPermitLimit"] = "1",
        });
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/pairing/start", new { deviceName = "", kind = "desktop" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
