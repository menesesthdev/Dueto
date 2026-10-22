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
}
