using System.Security.Claims;
using Dueto.Api.Authentication;
using Dueto.Api.Contracts;
using Dueto.Core.Catalog;
using Dueto.Core.State;

namespace Dueto.Api.Endpoints;

/// <summary>Estado do par (wallpaper atual, versão e confirmações). Exige token de dispositivo.</summary>
public static class StateEndpoints
{
    public static IEndpointRouteBuilder MapStateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/state").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, IStateService state, CancellationToken cancellationToken) =>
            Results.Ok(await state.GetAsync(user.ToDeviceIdentity(), cancellationToken)));

        group.MapPut("/", async (
            SetStateRequest? request,
            ClaimsPrincipal user,
            IStateService state,
            CancellationToken cancellationToken) =>
        {
            if (!WallpaperId.IsValid(request?.WallpaperId))
            {
                return InvalidWallpaperId();
            }

            var result = await state.SetAsync(user.ToDeviceIdentity(), request!.WallpaperId!, cancellationToken);

            return result.Status switch
            {
                SetStateStatus.Success => Results.Ok(new SetStateResponse(result.Version)),
                SetStateStatus.AnimatedNotAllowedOnMobile => AnimatedIsDesktopOnly(),
                _ => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Wallpaper not found."),
            };
        });

        group.MapPost("/applied", async (
            AppliedRequest? request,
            ClaimsPrincipal user,
            IStateService state,
            CancellationToken cancellationToken) =>
        {
            if (!WallpaperId.IsValid(request?.WallpaperId))
            {
                return InvalidWallpaperId();
            }

            var status = await state.ApplyAsync(
                user.ToDeviceIdentity(),
                request!.WallpaperId!,
                request.Version,
                cancellationToken);

            return status switch
            {
                ApplyStatus.Success => Results.NoContent(),
                ApplyStatus.WallpaperNotFound => Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Wallpaper not found."),
                ApplyStatus.AnimatedNotAllowedOnMobile => AnimatedIsDesktopOnly(),
                ApplyStatus.Mismatch => Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The current version of the pair is a different wallpaper."),
                _ => Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid version.",
                    detail: "The version must be between 1 and the current version of the pair."),
            };
        });

        return app;
    }

    private static IResult AnimatedIsDesktopOnly() => Results.Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: "Animated wallpapers are available only on desktop.");

    private static IResult InvalidWallpaperId() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid wallpaper id.",
        detail: "The id must match ^[a-z0-9-]{1,64}$.");
}
