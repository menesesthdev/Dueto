using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Dueto.Tests.Infrastructure;

/// <summary>
/// API em memória com configuração customizada e sem banco real. Serve para
/// testar CORS e rate limit usando rotas que respondem antes de tocar no banco.
/// </summary>
public sealed class ConfiguredApiFactory(IReadOnlyDictionary<string, string?> settings)
    : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Database=dueto_security_test",
            });
            config.AddInMemoryCollection(settings);
        });
        return base.CreateHost(builder);
    }
}
