using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dueto.Infrastructure.Persistence;

/// <summary>
/// Fábrica usada só em tempo de design (ferramenta <c>dotnet ef</c>) para
/// gerar e aplicar migrations. Lê a string de conexão da variável de ambiente
/// <c>Database__ConnectionString</c>; se não houver, cai num padrão de
/// desenvolvimento local (o mesmo do docker-compose.dev.yml). Nunca usada em runtime.
/// </summary>
public sealed class DuetoDbContextFactory : IDesignTimeDbContextFactory<DuetoDbContext>
{
    private const string LocalDevConnectionString =
        "Host=localhost;Port=5432;Database=dueto;Username=dueto;Password=dueto";

    public DuetoDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("Database__ConnectionString")
            ?? LocalDevConnectionString;

        var options = new DbContextOptionsBuilder<DuetoDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DuetoDbContext(options);
    }
}
