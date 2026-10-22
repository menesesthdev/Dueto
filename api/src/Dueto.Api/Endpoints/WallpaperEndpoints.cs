using Dueto.Core.Catalog;

namespace Dueto.Api.Endpoints;

/// <summary>Endpoints públicos do catálogo de wallpapers.</summary>
public static class WallpaperEndpoints
{
    public static IEndpointRouteBuilder MapWallpaperEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/wallpapers");

        group.MapGet("/", async (IWallpaperCatalog catalog, CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ListAsync(cancellationToken)));

        group.MapGet("/{id}", async (string id, IWallpaperCatalog catalog, CancellationToken cancellationToken) =>
        {
            if (!WallpaperId.IsValid(id))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid wallpaper id.",
                    detail: "The id must match ^[a-z0-9-]{1,64}$.");
            }

            var wallpaper = await catalog.FindAsync(id, cancellationToken);

            return wallpaper is null
                ? Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Wallpaper not found.")
                : Results.Ok(wallpaper);
        });

        return app;
    }
}
