using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dueto.Api.Security;

/// <summary>Registro de CORS e rate limit.</summary>
public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddDuetoSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<CorsPolicyOptions>()
            .Bind(configuration.GetSection(CorsPolicyOptions.SectionName))
            .Validate(
                o => o.AllowedOrigins.All(CorsPolicyOptions.IsValidOrigin),
                "Cors:AllowedOrigins must contain only origins like https://host (no wildcard, no path; http only for localhost).")
            .ValidateOnStart();

        services
            .AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<CorsPolicyOptions>>((cors, policy) =>
            {
                cors.AddPolicy(CorsPolicyOptions.PolicyName, builder => builder
                    .WithOrigins(policy.Value.AllowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .WithHeaders("Authorization", "Content-Type", "X-SignalR-User-Agent"));
            });

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiter, settings) =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                limiter.AddPolicy(RateLimitingOptions.PublicPolicyName, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.Value.PublicPermitLimit,
                            Window = TimeSpan.FromSeconds(settings.Value.PublicWindowSeconds),
                            QueueLimit = 0,
                        }));

                limiter.AddPolicy(RateLimitingOptions.PairingPolicyName, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.Value.PairingPermitLimit,
                            Window = TimeSpan.FromSeconds(settings.Value.PairingWindowSeconds),
                            QueueLimit = 0,
                        }));

                // Uma única janela para todos os IPs, só em pairing/complete.
                limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    IsPairingComplete(httpContext.Request)
                        ? RateLimitPartition.GetFixedWindowLimiter(
                            "pairing-complete-global",
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = settings.Value.PairingCompleteGlobalPermitLimit,
                                Window = TimeSpan.FromSeconds(settings.Value.PairingWindowSeconds),
                                QueueLimit = 0,
                            })
                        : RateLimitPartition.GetNoLimiter("none"));

                limiter.OnRejected = async (context, cancellationToken) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                    }

                    await Results
                        .Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests.")
                        .ExecuteAsync(context.HttpContext);
                };
            });

        return services;
    }

    private static bool IsPairingComplete(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && request.Path.Value?.TrimEnd('/').Equals("/api/pairing/complete", StringComparison.OrdinalIgnoreCase) == true;
}
