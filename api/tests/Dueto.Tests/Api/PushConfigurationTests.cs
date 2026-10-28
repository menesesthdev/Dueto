using System.Net;
using Dueto.Tests.Infrastructure;

namespace Dueto.Tests.Api;

/// <summary>Configuração do Web Push. Não precisa de banco: as rotas usadas respondem antes dele.</summary>
public class PushConfigurationTests
{
    [Fact]
    public async Task Vapid_public_key_returns_503_when_push_is_not_configured()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>());

        var response = await factory.CreateClient().GetAsync("/api/push/vapid-public-key");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void App_refuses_to_start_with_only_one_vapid_key()
    {
        using var factory = new ConfiguredApiFactory(new Dictionary<string, string?>
        {
            ["Push:VapidPublicKey"] = "only-the-public-one",
            ["Push:VapidSubject"] = "mailto:owner@example.com",
        });

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }
}
