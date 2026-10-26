using System.ComponentModel.DataAnnotations;

namespace Dueto.Api.Security;

/// <summary>
/// Limite de requisições por IP nos endpoints públicos (janela fixa).
/// Configurável por <c>RateLimiting__PublicPermitLimit</c> e <c>RateLimiting__PublicWindowSeconds</c>.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public const string PublicPolicyName = "public";
    public const string PairingPolicyName = "pairing";

    [Range(1, 10_000)]
    public int PublicPermitLimit { get; set; } = 60;

    [Range(1, 3_600)]
    public int PublicWindowSeconds { get; set; } = 60;

    /// <summary>Requisições por IP nos endpoints de pareamento. Estrito: o código tem só 6 dígitos.</summary>
    [Range(1, 1_000)]
    public int PairingPermitLimit { get; set; } = 5;

    [Range(1, 3_600)]
    public int PairingWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Teto GLOBAL (todos os IPs somados) para <c>pairing/complete</c>. Limita a força bruta
    /// do código mesmo que o atacante troque de IP.
    /// </summary>
    [Range(1, 10_000)]
    public int PairingCompleteGlobalPermitLimit { get; set; } = 30;
}
