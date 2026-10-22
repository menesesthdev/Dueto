using Dueto.Core.Domain;
using Dueto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dueto.Tests.Persistence;

/// <summary>
/// Testes do mapeamento EF. Não abrem conexão: só inspecionam o modelo
/// construído, validando as configurações por entidade.
/// </summary>
public class DuetoDbContextModelTests
{
    private static DuetoDbContext CreateContext()
    {
        // String de conexão fictícia: UseNpgsql não abre conexão só para
        // construir o modelo, então nenhum banco é necessário aqui.
        var options = new DbContextOptionsBuilder<DuetoDbContext>()
            .UseNpgsql("Host=localhost;Database=dueto_model_test")
            .Options;
        return new DuetoDbContext(options);
    }

    [Theory]
    [InlineData(typeof(Wallpaper), "wallpapers")]
    [InlineData(typeof(Pair), "pairs")]
    [InlineData(typeof(PairingCode), "pairing_codes")]
    [InlineData(typeof(Device), "devices")]
    [InlineData(typeof(PairState), "pair_state")]
    [InlineData(typeof(DeviceApplied), "device_applied")]
    [InlineData(typeof(PushSubscription), "push_subscriptions")]
    [InlineData(typeof(Reminder), "reminders")]
    public void Entity_maps_to_expected_snake_case_table(Type clrType, string expectedTable)
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(clrType);

        Assert.NotNull(entityType);
        Assert.Equal(expectedTable, entityType!.GetTableName());
    }

    [Fact]
    public void Columns_are_snake_case()
    {
        using var context = CreateContext();

        var wallpaper = context.Model.FindEntityType(typeof(Wallpaper))!;
        var columnNames = wallpaper.GetProperties()
            .Select(p => p.GetColumnName())
            .ToArray();

        Assert.Contains("desktop_url", columnNames);
        Assert.Contains("desktop_sha256", columnNames);
        Assert.Contains("thumbnail_url", columnNames);
        Assert.Contains("created_at", columnNames);
    }

    [Fact]
    public void Enum_properties_are_stored_as_text()
    {
        using var context = CreateContext();

        var wallpaper = context.Model.FindEntityType(typeof(Wallpaper))!;
        var kind = wallpaper.FindProperty(nameof(Wallpaper.Kind))!;

        // HasConversion<string>() => a coluna é textual, não inteira.
        Assert.Equal(typeof(string), kind.GetProviderClrType());
    }
}
