using System.ComponentModel.DataAnnotations;

namespace Dueto.Core.Pairing;

/// <summary>Configuração do pareamento. Variável de ambiente: <c>Pairing__CodeTtlMinutes</c>.</summary>
public sealed class PairingOptions
{
    public const string SectionName = "Pairing";

    /// <summary>Validade do código de pareamento, em minutos.</summary>
    [Range(1, 60)]
    public int CodeTtlMinutes { get; set; } = 5;
}
