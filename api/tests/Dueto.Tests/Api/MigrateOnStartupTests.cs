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
    private static readonly string SampleCatalog = Path.Combine(AppContext.BaseDirectory, "TestData", "catalog.sample.json");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private WebApplicationFactory<Program> Factory(bool migrateOnStartup, string? catalogFile = null) =>
        new FactoryForMigration(_postgres.GetConnectionString(), migrateOnStartup, catalogFile);

    [Fact]
    public async Task An_empty_database_is_migrated_and_the_catalog_starts_empty()
    {
        using var factory = Factory(migrateOnStartup: true);

        var response = await factory.CreateClient().GetAsync("/api/wallpapers");
        var wallpapers = await response.Content.ReadFromJsonAsync<List<WallpaperDto>>();

        // O seed fictício foi removido: o catálogo só tem o que o catalog.json trouxer.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(wallpapers!);
    }

    [Fact]
    public async Task The_dev_sample_catalog_is_imported_on_startup()
    {
        using var factory = Factory(migrateOnStartup: true, catalogFile: SampleCatalog);

        var wallpapers = await factory.CreateClient().GetFromJsonAsync<List<WallpaperDto>>("/api/wallpapers");

        Assert.Equal(["drift-loop", "neon-tide", "aurora-dunes"], wallpapers!.Select(w => w.Id));
    }

    [Fact]
    public async Task Starting_again_on_an_already_migrated_database_is_harmless()
    {
        using (var first = Factory(migrateOnStartup: true, catalogFile: SampleCatalog))
        {
            await first.CreateClient().GetAsync("/health");
        }

        using var second = Factory(migrateOnStartup: true, catalogFile: SampleCatalog);
        var wallpapers = await second.CreateClient().GetFromJsonAsync<List<WallpaperDto>>("/api/wallpapers");

        Assert.Equal(3, wallpapers!.Count); // o upsert não duplica
    }

    [Fact]
    public async Task Nothing_is_migrated_when_the_option_is_off()
    {
        using var factory = Factory(migrateOnStartup: false);

        // O banco está vazio e ninguém migrou: a tabela não existe, então a consulta falha com 500 (ProblemDetails).
        var response = await factory.CreateClient().GetAsync("/api/wallpapers");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private sealed class FactoryForMigration(string connectionString, bool migrateOnStartup, string? catalogFile)
        : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = connectionString,
                    ["Database:MigrateOnStartup"] = migrateOnStartup ? "true" : "false",
                };

                if (catalogFile is not null)
                {
                    settings["Catalog:ImportFile"] = catalogFile;
                    settings["Catalog:AllowedHosts:0"] = "example.com";
                }

                config.AddInMemoryCollection(settings);
            });
            return base.CreateHost(builder);
        }
    }
}
