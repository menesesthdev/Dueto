using Dueto.Api.Contracts;
using Dueto.Api.Security;
using Dueto.Core.Domain;
using Dueto.Core.Pairing;

namespace Dueto.Api.Endpoints;

/// <summary>Pareamento por código curto. Endpoints públicos, com rate limit estrito.</summary>
public static class PairingEndpoints
{
    private const int MaxDeviceNameLength = 100;

    public static IEndpointRouteBuilder MapPairingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pairing")
            .RequireRateLimiting(RateLimitingOptions.PairingPolicyName);

        group.MapPost("/start", async (
            StartPairingRequest? request,
            HttpContext http,
            IPairingService pairing,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            var name = ValidateName(request?.DeviceName, errors);
            var kind = ValidateKind(request?.Kind, errors);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var started = await pairing.StartAsync(kind, name, cancellationToken);

            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new StartPairingResponse(started.DeviceToken, started.Code, started.ExpiresAt));
        });

        group.MapPost("/complete", async (
            CompletePairingRequest? request,
            HttpContext http,
            IPairingService pairing,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (!PairingCodes.IsValidFormat(request?.Code))
            {
                errors["code"] = [$"The code must have exactly {PairingCodes.Length} digits."];
            }

            var name = ValidateName(request?.DeviceName, errors);
            var kind = ValidateKind(request?.Kind, errors);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await pairing.CompleteAsync(request!.Code!, kind, name, cancellationToken);

            http.Response.Headers.CacheControl = "no-store";
            return result.Status switch
            {
                PairingCompleteStatus.Success => Results.Ok(new CompletePairingResponse(result.DeviceToken!)),
                PairingCompleteStatus.KindConflict => Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "This pair already has a device of that kind."),
                _ => Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid or expired pairing code."),
            };
        });

        return app;
    }

    private static string ValidateName(string? value, Dictionary<string, string[]> errors)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is 0 or > MaxDeviceNameLength)
        {
            errors["deviceName"] = [$"The device name must have between 1 and {MaxDeviceNameLength} characters."];
        }

        return name;
    }

    private static DeviceKind ValidateKind(string? value, Dictionary<string, string[]> errors)
    {
        if (!DeviceKinds.TryParse(value, out var kind))
        {
            errors["kind"] = ["The kind must be \"desktop\" or \"mobile\"."];
        }

        return kind;
    }
}
