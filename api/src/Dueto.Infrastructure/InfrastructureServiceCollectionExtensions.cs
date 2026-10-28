using Dueto.Core.Catalog;
using Dueto.Core.Pairing;
using Dueto.Core.Push;
using Dueto.Core.State;
using Dueto.Infrastructure.Catalog;
using Dueto.Infrastructure.Pairing;
using Dueto.Infrastructure.Persistence;
using Dueto.Infrastructure.Push;
using Dueto.Infrastructure.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dueto.Infrastructure;

/// <summary>Registro de DI da camada de infraestrutura.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDuetoInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<DuetoDbContext>((sp, options) =>
        {
            var database = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(database.ConnectionString);
        });

        services
            .AddOptions<PairingOptions>()
            .Bind(configuration.GetSection(PairingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<PushOptions>()
            .Bind(configuration.GetSection(PushOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.Validate() is null, "Invalid Push configuration. See Push:* settings.")
            .ValidateOnStart();

        services.AddScoped<IWallpaperCatalog, WallpaperCatalog>();
        services.AddScoped<IPairingService, PairingService>();
        services.AddScoped<IDeviceAuthenticator, DeviceAuthenticator>();
        services.AddScoped<IStateService, StateService>();

        services.AddSingleton<IPushSender, WebPushSender>();
        services.AddScoped<IPushDispatcher, PushDispatcher>();
        services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();
        services.AddScoped<IReminderProcessor, ReminderProcessor>();
        services.AddHostedService<ReminderScheduler>();

        return services;
    }

    /// <summary>Aplica as migrations pendentes se <c>Database:MigrateOnStartup</c> estiver ligado.</summary>
    public static async Task ApplyMigrationsIfEnabledAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (!services.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DuetoDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
