using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Dueto.Tests.Api;

/// <summary>
/// Sobe a API em memória (sem servidor de verdade) e confere o /health.
/// Fornece uma string de conexão fictícia só para a app iniciar: o health
/// check não toca no banco, então nenhum Postgres é necessário.
/// </summary>
public class HealthEndpointTests : IClassFixture<HealthEndpointTests.DuetoApiFactory>
{
    private readonly DuetoApiFactory _factory;

    public HealthEndpointTests(DuetoApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_health_returns_ok_and_healthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }

    public sealed class DuetoApiFactory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=localhost;Database=dueto_health_test",
                });
            });
            return base.CreateHost(builder);
        }
    }
}
