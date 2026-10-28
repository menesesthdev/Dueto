using Dueto.Core.Push;
using Dueto.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Dueto.Tests.Infrastructure;

/// <summary>
/// Sobe a API em memória apontando para um Postgres real e descartável em
/// container, com as migrations (e o seed) já aplicadas. Requer Docker.
/// Um único container é compartilhado por todas as classes da coleção "postgres".
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public TestClock Clock { get; } = new();

    public FakePushSender PushSender { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Aplica as migrations antes de qualquer request.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DuetoDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = _postgres.GetConnectionString(),

                // Os testes fazem muitas chamadas do mesmo "IP"; limites altos para não interferirem.
                ["RateLimiting:PublicPermitLimit"] = "10000",
                ["RateLimiting:PairingPermitLimit"] = "1000",
                ["RateLimiting:PairingCompleteGlobalPermitLimit"] = "10000",

                // Os testes disparam o processador de lembretes à mão; o agendador de fundo fica quase parado.
                ["Push:SchedulerIntervalSeconds"] = "3600",
            });
        });
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            services.RemoveAll<IPushSender>();
            services.AddSingleton<IPushSender>(PushSender);
        });

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
