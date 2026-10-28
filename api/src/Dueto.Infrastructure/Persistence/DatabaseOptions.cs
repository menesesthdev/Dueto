using System.ComponentModel.DataAnnotations;

namespace Dueto.Infrastructure.Persistence;

/// <summary>
/// Configuração de acesso ao banco. A string de conexão nunca fica no código
/// nem no repositório: vem de variável de ambiente (<c>Database__ConnectionString</c>).
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Aplica as migrations pendentes ao iniciar. Desligado por padrão; em produção a imagem não tem o SDK
    /// (<c>dotnet ef</c>), então o compose liga com <c>Database__MigrateOnStartup=true</c>. Pensado para uma única instância.
    /// </summary>
    public bool MigrateOnStartup { get; set; }
}
