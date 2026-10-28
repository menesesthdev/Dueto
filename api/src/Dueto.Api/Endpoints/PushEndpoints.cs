using System.Security.Claims;
using System.Text.RegularExpressions;
using Dueto.Api.Authentication;
using Dueto.Api.Contracts;
using Dueto.Api.Security;
using Dueto.Core.Domain;
using Dueto.Core.Push;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dueto.Api.Endpoints;

/// <summary>Assinatura Web Push do celular e a chave pública VAPID.</summary>
public static partial class PushEndpoints
{
    private const int MaxKeyLength = 256;

    [GeneratedRegex(@"^[A-Za-z0-9_-]+={0,2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Base64UrlPattern();

    public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        // A chave pública não é segredo; o PWA precisa dela antes de pedir a assinatura ao navegador.
        app.MapGet("/api/push/vapid-public-key", (IPushSender sender) =>
                sender.VapidPublicKey is { } key
                    ? Results.Ok(new VapidPublicKeyResponse(key))
                    : Results.Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Web Push is not configured."))
            .RequireRateLimiting(RateLimitingOptions.PublicPolicyName);

        var group = app.MapGroup("/api/push/subscriptions").RequireAuthorization();

        group.MapPost("/", async (
            SubscribePushRequest? request,
            ClaimsPrincipal user,
            IOptions<PushOptions> options,
            IPushSubscriptionService subscriptions,
            CancellationToken cancellationToken) =>
        {
            var identity = user.ToDeviceIdentity();
            if (identity.Kind != DeviceKind.Mobile)
            {
                return OnlyMobile();
            }

            var errors = new Dictionary<string, string[]>();
            if (!options.Value.IsAllowedEndpoint(request?.Endpoint))
            {
                errors["endpoint"] = ["The endpoint must be an https URL of a known push service."];
            }

            if (!IsValidKey(request?.Keys?.P256dh))
            {
                errors["keys.p256dh"] = ["Invalid key."];
            }

            if (!IsValidKey(request?.Keys?.Auth))
            {
                errors["keys.auth"] = ["Invalid key."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            await subscriptions.SubscribeAsync(
                identity,
                new PushTarget(request!.Endpoint!, request.Keys!.P256dh!, request.Keys.Auth!),
                cancellationToken);

            return Results.NoContent();
        });

        group.MapDelete("/", async (
            [FromBody] UnsubscribePushRequest? request,
            ClaimsPrincipal user,
            IPushSubscriptionService subscriptions,
            CancellationToken cancellationToken) =>
        {
            var identity = user.ToDeviceIdentity();
            if (identity.Kind != DeviceKind.Mobile)
            {
                return OnlyMobile();
            }

            if (string.IsNullOrWhiteSpace(request?.Endpoint) || request.Endpoint.Length > 2_048)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["endpoint"] = ["The endpoint is required."],
                });
            }

            await subscriptions.UnsubscribeAsync(identity, request.Endpoint, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }

    private static IResult OnlyMobile() => Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Only mobile devices can manage push subscriptions.");

    private static bool IsValidKey(string? value) =>
        value is { Length: > 0 and <= MaxKeyLength } && Base64UrlPattern().IsMatch(value);
}
