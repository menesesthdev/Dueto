namespace Dueto.Core.Domain;

/// <summary>
/// Código curto de pareamento, de uso único e validade curta. Liga um segundo
/// dispositivo a um par já criado.
/// </summary>
public class PairingCode
{
    /// <summary>O código em si (curto). Chave primária.</summary>
    public required string Code { get; set; }

    public Guid PairId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Quando foi consumido. Nulo enquanto ainda não usado.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}
