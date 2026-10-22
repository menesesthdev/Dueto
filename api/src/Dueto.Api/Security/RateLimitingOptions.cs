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

    [Range(1, 10_000)]
    public int PublicPermitLimit { get; set; } = 60;

    [Range(1, 3_600)]
    public int PublicWindowSeconds { get; set; } = 60;
}
