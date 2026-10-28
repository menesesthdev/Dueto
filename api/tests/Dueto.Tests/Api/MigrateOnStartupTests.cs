using System.Net;
using System.Net.Http.Json;
using Dueto.Core.Catalog;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Dueto.Tests.Api;

/// <summary>
/// Em produção a imagem não tem o SDK (<c>dotnet ef</c>), então a API aplica as migrations ao iniciar quando
/// <c>Database__MigrateOnStartup=true</c>. Testado contra um Postgres VAZIO, num container próprio.
/// </summary>
public sealed class MigrateOnStartupTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private WebApplicationFactory<Program> Factory(bool migrateOnStartup) =>
        new WebApplicationFactoryForMigration(_postgres.GetConnectionString(), migrateOnStartup);

    [Fact]
    public async Task An_empty_database_is_migrated_when_the_option_is_on()
    {
        using var factory = Factory(migrateOnStartup: true);

        var response = await factory.CreateClient().GetAsync("/api/wallpapers");
        var wallpapers = await response.Content.ReadFromJsonAsync<List<WallpaperDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, wallpapers!.Count);
    }

    [Fact]
    public async Task Starting_again_on_an_already_migrated_database_is_harmless()
    {
        using (var first = Factory(migrateOnStartup: true))
        {
            await first.CreateClient().GetAsync("/health");
        }

        using var second = Factory(migrateOnStartup: true);
        var response = await second.CreateClient().GetAsync("/api/wallpapers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Nothing_is_migrated_when_the_option_is_off()
    {
        using var factory = Factory(migrateOnStartup: false);

        // O banco está vazio e ninguém migrou: a tabela não existe, então a consulta falha com 500 (ProblemDetails).
        var response = await factory.CreateClient().GetAsync("/api/wallpapers");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private sealed class WebApplicationFactoryForMigration(string connectionString, bool migrateOnStartup)
        : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = connectionString,
                    ["Database:MigrateOnStartup"] = migrateOnStartup ? "true" : "false",
                });
            });
            return base.CreateHost(builder);
        }
    }
}
